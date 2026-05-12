---
type: spike
status: draft
tags: [#spike, #runtime, #lifecycle, #walkthrough]
---

# Runtime Walkthrough — End-to-End Game Lifecycle

> **Status: draft / exploratory.** A temporal walk through a generic card-game session, from `GameFactory.Build` to `EndedSession`. Uses a concrete "play a card, draw a card, sometimes target" running example. Captures the locked runtime behavior inline; remaining open questions surface as `⚠ Q1`–`Q3` callouts.

## Why this exists

Each feature is settled in isolation. This spike walks a full match end-to-end against the locked design to confirm the *interactions between features* compose, and to surface what's still underspecified before sample / vertical-slice work bakes in implementation choices that paper over gaps.

## Cast

The systems involved, named as they appear in diagrams:

| Symbol | What it is |
|---|---|
| **Player** | Human at a device |
| **Device** | Raw input source (mouse / touch / keyboard) |
| **Gateway** | `IntentGateway` — raw gesture → `PlayerIntent` → `engine.Dispatch` |
| **VM** | Active `MatchStateViewModel<TState>` |
| **Registry** | `ViewModelRegistry` |
| **Presenter** | Subscribes to `MatchStateSlice`; manages active VM |
| **Engine** | `GameEngine` — orchestrator: drain loop, FSM evaluation, session field, holds the three pipelines |
| **Validators** | Registered `ICommandValidator<T>` chain |
| **Store** | `Scaffold.Store` |
| **Slices** | `BaseState` subtypes (named when relevant) |
| **Handler** | `GameCommandHandler<TCommand, TResult>` |
| **Triggers** | `ITriggerPipeline` — engine invokes `RunBefore` / `RunAfter` during drain |
| **Effects** | `IEffect.Run(scope)` coroutines, queued in `PendingEffectsSlice` |
| **Runtime** | `GameRulesRuntime<T>` — owns `Evaluate(action, result) → MatchState` |

## Running example

A generic 2-player card game:

- **Players:** `Player1`, `Player2`.
- **Zones per player:** `Deck`, `Hand`, `Battlefield`.
- **Match states:** `Player1TurnState` ⇄ `Player2TurnState` → `EndOfMatchState`.
- **Sample action:** `PlayCardAction(actor, card, fromZone, toZone)` — moves a card from hand to battlefield.
- **Sample triggered effect:** "on play, draw a card" — pushes `DrawCardEffect` to `PendingEffectsSlice`.
- **Sample request flow:** a card with "on play, target a card on the opponent's battlefield" — uses `Request<Card>` / `Response`.

---

## Phase 1: Boot

The host wires the engine. No match running yet.

```mermaid
sequenceDiagram
  participant Host
  participant Factory as GameFactory
  participant Engine
  participant Store
  participant Presenter
  participant Gateway

  Host->>Factory: Build(GameDefinition, MatchSetup)
  Factory->>Store: new Store
  Factory->>Store: seed framework slices
  Factory->>Store: seed game-singleton slices
  Factory->>Store: seed per-spawn slices (cards, zones, players)
  Factory->>Store: apply MatchSetup.InitialSlices overlay
  Factory->>Engine: new GameEngine(store, runtime, pipelines)
  Engine->>Engine: engine.Session = NotStartedSession
  Host->>Presenter: new Presenter(engine, registry)
  Presenter->>Store: subscribe to MatchStateSlice
  Host->>Gateway: new IntentGateway(engine, presenter)
```

**State after boot:**

- `engine.Session`: `NotStartedSession` (typed field on `GameEngine`, not a Store slice).
- Framework slices: seeded at initial values (empty queues, no history, no requests, no answers).
- Game-author slices: seeded structure but no gameplay state.
- `MatchStateSlice`: **not seeded yet** — `StartMatchHandler` seeds it via `TransitionStateCommand` on first dispatch. `MatchStateActionGate` is session-aware: it's skipped in `NotStartedSession`, so the absence is fine.

**System touches:** *State* (Store seeded) → *Engine* (constructed, session field set) → *Presentation* (presenter subscribes; no active VM yet).

---

## Phase 2: Start match

Host issues `StartMatchCommand`. Session flips, the initial `MatchState` lands, setup effects (if any) drain.

```mermaid
sequenceDiagram
  participant Host
  participant Engine
  participant Validators
  participant Handler as StartMatchHandler
  participant Store
  participant Triggers
  participant Effects as PendingEffectsSlice
  participant Runtime
  participant Presenter
  participant Registry

  Host->>Engine: Dispatch(StartMatchCommand)
  Engine->>Validators: chain
  Note over Validators: SessionGate ✓ (NotStartedSession admits Start) · MatchStateActionGate bypassed (lifecycle command)
  Engine->>Handler: execute
  Handler->>Engine: engine.Session = InMatchSession (framework-internal field set)
  Handler->>Store: scope.Dispatch(MatchStartedSignal)
  Engine->>Runtime: Evaluate(StartMatchCommand, result)
  Runtime-->>Engine: ITurnFlow.Initial (e.g. Player1TurnState)
  Engine->>Engine: synthesize + Dispatch(TransitionStateCommand)
  Note over Engine: TransitionStateHandler writes MatchStateSlice; Player1TurnState.EnterCommands fire
  Triggers->>Effects: OnAfter<MatchStartedSignal> reactions may push SetupEffect
  Engine->>Effects: drain pending effects (setup effects, then EnterCommands)
  Engine-->>Presenter: MatchStateSlice → Player1TurnState
  Presenter->>Registry: Create(Player1TurnState)
  Registry-->>Presenter: Player1TurnVM (bound)
```

**Slice + field updates:**

- `engine.Session`: `NotStartedSession` → `InMatchSession`.
- `MatchStateSlice.Current`: not seeded → `Player1TurnState` (via `TransitionStateCommand` cascade).
- `HistoryState`: `StartMatchCommand` · `MatchStartedSignal` · `TransitionStateCommand` envelopes + whatever setup effects/`EnterCommands` add.
- `PendingEffectsSlice`: temporarily holds setup effects; drains.

**Setup origin.** Two natural channels:

- **`MatchState.EnterCommands`** on the initial state — emitted when the state is entered. Use for deterministic per-state setup commands.
- **`OnAfter<MatchStartedSignal>` triggers** — react to the signal `StartMatchHandler` emits. Use for effect-flavored setup (deal + shuffle + initial mulligan etc.) by pushing a `SetupEffect` to `PendingEffectsSlice`.

**Rebind timing.** Slice subscribers fire on **command boundaries** — one command = one observable snapshot. Setup commands each tick their own notification. The presenter sees `MatchStateSlice` updated as soon as `TransitionStateHandler` runs and rebinds to `Player1TurnVM` immediately; setup effects drain afterward and the VM's observables react per command. Pacing (cinematic mid-deal UI vs hold-until-settled) lives in the VM, not the framework.

**System touches:** *Actions* (validator chain, handler) → *Engine* (field set; emits signal; FSM Evaluate; synthesizes TransitionStateCommand) → *Triggers* (`OnAfter<MatchStartedSignal>` reactions push effects) → *Effects* (setup drain) → *State* (slices mutated via fan-out mutators) → *Presentation* (VM rebind).

---

## Phase 3: Simple action (play a card)

Player1's turn. They drag a card from hand to the battlefield. The card has "on play, draw a card."

```mermaid
sequenceDiagram
  participant Player
  participant Device
  participant Gateway
  participant VM as Player1TurnVM
  participant Engine
  participant Validators
  participant Triggers
  participant Handler as PlayCardHandler
  participant Store
  participant Effects as PendingEffectsSlice
  participant Runtime

  Player->>Device: drag card → battlefield
  Device->>Gateway: raw gesture
  Gateway->>Gateway: translate → PlayCardIntent
  Gateway->>VM: Map(PlayCardIntent)
  VM-->>Gateway: PlayCardAction(Player1, card, Hand, Battlefield)
  Gateway->>Engine: Dispatch(PlayCardAction)
  Engine->>Validators: chain
  Note over Validators: SessionGate ✓ · MatchStateActionGate ✓ (PlayCardAction in Player1TurnState.Actions) · game validators ✓
  Engine->>Triggers: RunBefore(PlayCardAction, scope)
  Engine->>Handler: execute
  Handler->>Store: Execute(new PlayCardPayload(card, Hand, Battlefield))
  Note over Store: fan-out mutators write ZoneContentsState, LastMoveState (one payload, N mutators)
  Engine->>Triggers: RunAfter(PlayCardAction, result, scope)
  Triggers->>Effects: enqueue DrawCardEffect (from OnAfter<PlayCardAction> reaction)
  Engine->>Runtime: Evaluate(PlayCardAction, result)
  Runtime-->>Engine: Player1TurnState (no transition)
  Engine->>Effects: drain DrawCardEffect
  Effects->>Engine: await scope.Dispatch(DrawCardAction)
  Note over Engine: DrawCardAction runs the full pipeline (validators, fold, handler, fold, Evaluate)
  Engine-->>VM: slice subscriptions fire at each command boundary
```

**Locked behavior:**

- **Engine invokes the trigger fold.** Engine holds `ITriggerPipeline` and calls `RunBefore` / `RunAfter` around the handler. Trigger feature filters its own registry via per-trigger `Trigger<T>.Validate`; no upstream filter call.
- **Single payload, multiple mutators.** Handler dispatches one semantic payload (`PlayCardPayload`); N mutators register against it (one per slice that reacts). `Store.Execute(payload)` fans out — each mutator writes its slice. Adding a slice that reacts = register one more mutator; handler unchanged.
- **`CompositeMutation`** is the alternative when a reusable cluster of mutations needs to expand inline (e.g., a shared "move card + record last-moved" cluster). It is **not** a `GameCommand` — lives outside the family. Reached via `scope.Execute(CompositeMutation)` rather than `scope.Dispatch(...)`. The composite's `Apply(scope)` invokes its sub-mutations directly in the current scope; no queue, no envelope, no fold.
- **Effects fire through the engine drain only.** Trigger reactions push to `PendingEffectsSlice`; engine drains. Effects use `scope.Dispatch(GameCommand)` for commands and `scope.Execute(CompositeMutation)` for inline mutation clusters — two semantically distinct surfaces.

**Slice updates:**

- `ZoneContentsState`: `Hand[Player1]` -= card; `Battlefield[Player1]` += card; then `Deck[Player1]` -= top, `Hand[Player1]` += top.
- `LastMoveState`: records both moves.
- `HistoryState`: 2 envelopes (`PlayCardAction`, `DrawCardAction`).
- `MatchStateSlice.Current`: unchanged.

**System touches:** *Presentation* (gesture → intent → command) → *Actions* (validators, handler, history) → *Triggers* (engine invokes fold; reactions push effects) → *Effects* (engine drains; effects dispatch their own commands through scope) → *Engine FSM* (Evaluate confirms no transition) → *State* (slices updated via fan-out; subscribers react at command boundaries via `scope.Subscribe<T>(handler) → IDisposable`; VM owns disposal through its host MVVM toolkit's lifecycle).

---

## Phase 4: Action with a request (play a card that targets)

Player1 plays a card whose effect is "on play, target a card on the opponent's battlefield; damage it."

```mermaid
sequenceDiagram
  participant Player
  participant Gateway
  participant VM as Player1TurnVM
  participant Engine
  participant Effects as PendingEffectsSlice
  participant Effect as TargetingEffect
  participant Slices as Requests/Answers

  Player->>Gateway: play targeting card
  Gateway->>Engine: Dispatch(PlayCardAction)
  Note over Engine: same as Phase 3 — validators, fold, handler, fold, fan-out mutators
  Engine->>Effects: enqueue TargetingEffect (OnAfter trigger reaction)
  Engine->>Effect: Run(scope)
  Effect->>Engine: await scope.Dispatch(new TargetRequest(predicate, options))
  Engine->>Triggers: RunBefore(TargetRequest, scope)
  Note over Triggers: OnBefore<TargetRequest> may redirect (replace target) or cancel (shroud / hexproof)
  Engine->>Handler: execute (writes RequestsSlice.Open = TargetRequest)
  Engine->>Triggers: RunAfter(TargetRequest, result, scope)
  Note over Triggers: OnAfter<TargetRequest> may push "when-targeted" reactions
  Note over Engine: drain pauses (RequestsSlice non-empty)
  Engine-->>VM: RequestsSlice updated
  VM->>VM: enter request-mode; expose target affordances
  Player->>Gateway: click target card
  Gateway->>VM: Map(TargetSelectedIntent)
  VM-->>Gateway: Response(requestId, targetCard)
  Gateway->>Engine: Dispatch(Response)
  Engine->>Triggers: RunBefore(Response, scope)
  Engine->>Handler: execute (writes AnswersSlice += answer · clears RequestsSlice.Open)
  Engine->>Triggers: RunAfter(Response, result, scope)
  Engine-->>Effect: await returns with typed Response
  Effect->>Engine: scope.Dispatch(DamageCardAction(target))
  Note over Engine: DamageCardAction runs full pipeline (validators, fold, handler, fold)
  Effect-->>Engine: complete
  Engine->>Effects: dequeue TargetingEffect
```

**Locked behavior:**

- `Request<TResponse>` and `Response` are **siblings** of `GameAction` (not subtypes). All four `GameCommand` siblings flow through `engine.Dispatch` uniformly.
- **Trigger fold runs for Request and Response.** `OnBefore<TargetRequest>` enables redirect / shroud / hexproof patterns; `OnAfter<TargetRequest>` enables "when-targeted" reactions; `OnBefore<Response>` can constrain the answer; `OnAfter<Response>` enables reactions on an answered request. Engine invokes the fold via `ITriggerPipeline.RunBefore` / `RunAfter` at the same step as for `GameAction`.
- Validator chain applies (`SessionGate` always; `MatchStateActionGate` and game-shipped validators per the chain). Because Request/Response are siblings (not subtypes), `ICommandValidator<GameAction>` and `OnBefore<GameAction>` registrations do NOT accidentally match Requests.
- **Cancellation is per-request policy + a flag on the typed Response.** `Request` base has `virtual bool AllowsCancellation => true`; specific mandatory-choice requests override to `false` (mulligan, must-pick). `Response` base has `bool Cancelled { get; init; }`. Player cancel gesture dispatches the typed response with `Cancelled = true` and default/null payload — same `scope.Dispatch(Response)` path as a normal answer. Effect checks `response.Cancelled` after the `await` and decides between fizzle (just `return`) or unwind (dispatch compensating actions). Match-end cancels every open request via the same flag (overrides per-request policy).
- **Data-only fields.** Requests and responses (and all `GameCommand` / `CompositeMutation` / payload / slice fields) are data-only — no `Func`, `Predicate`, delegate captures, or non-serializable handles. Constraints / option-filters on requests carry as pre-computed data (e.g., `IReadOnlyList<Ref<Card>> ValidTargets`) or as resolver-id + serializable params. Required for replay, save/load, and network round-trip.

**Slice updates across the request flow:**

- `RequestsSlice.Open`: null → `TargetRequest` → null.
- `AnswersSlice`: += answer entry (with `Cancelled` flag set if the player cancelled).
- `HistoryState`: `PlayCardAction` · `TargetRequest` · `Response` · `DamageCardAction` envelopes (or just `Response{Cancelled=true}` if the player cancelled).
- Plus whatever `DamageCardAction` mutates (skipped if the player cancelled).

**System touches:** *Effects* (orchestrates the multi-step flow via `await`; branches on `response.Cancelled`) → *Triggers* (engine invokes fold on Request and Response) → *State* (`RequestsSlice` + `AnswersSlice` are the rendezvous; `Open` is a dictionary supporting multiple parallel requests) → *Presentation* (VM enters request-mode; affordances change shape mid-turn; cancel affordance only when `AllowsCancellation = true`) → *Actions* (`Response` is a `GameCommand` sibling through the same `Dispatch`).

**Concurrent requests work via `Task.WhenAll`.** Multiple effects can each open their own request and park independently; the engine's "pause on open requests" only pauses **state transitions** (default `Evaluate` returns `Current`) — drain itself proceeds. Patterns: simultaneous mulligan, multi-actor response windows, parallel auctions. Authoring shape:

```csharp
var t1 = scope.Dispatch(new MulliganRequest(Player1, hand1));
var t2 = scope.Dispatch(new MulliganRequest(Player2, hand2));
await Task.WhenAll(t1, t2);
// process both responses
```

**Timeout is deferred to post-v1.** Design direction pinned: `Request` will gain optional `TimeSpan? Timeout`; framework `TimerService` dispatches the default `Cancelled = true` typed response on expiry. Replay reproduces deterministically because the recorded response carries the cancellation. Until shipped, games can wire their own timer service against `RequestsSlice` and dispatch `Cancelled = true` responses.

---

## Phase 5: Turn transition

Player1 clicks "end turn." The FSM transitions to `Player2TurnState`.

```mermaid
sequenceDiagram
  participant Player
  participant Gateway
  participant VM as Player1TurnVM
  participant Engine
  participant Runtime
  participant Slices
  participant Presenter
  participant Registry

  Player->>Gateway: click "end turn"
  Gateway->>VM: Map(EndTurnIntent)
  VM-->>Gateway: EndTurnAction(Player1)
  Gateway->>Engine: Dispatch(EndTurnAction)
  Engine->>Engine: validate, fold, drain handler (cleanup)
  Engine->>Runtime: Evaluate(EndTurnAction, result)
  Runtime-->>Engine: Player2TurnState (new state-type)
  Engine->>Engine: synthesize + Dispatch(TransitionStateCommand)
  Note over Engine: TransitionStateHandler writes MatchStateSlice; Player2TurnState.EnterCommands fire
  Engine->>Slices: MatchStateSlice.Current = Player2TurnState
  Engine-->>Presenter: MatchStateSlice updated (different state-type)
  Presenter->>Registry: Create(Player2TurnState)
  Registry-->>Presenter: Player2TurnVM (bound)
```

**Locked behavior:**

- **`Evaluate` returns a `MatchState`.** Engine compares to the current state-type. If the type differs, engine synthesizes `TransitionStateCommand(newState)` and dispatches it through the same pipeline.
- **Transitions are commands.** `TransitionStateCommand`'s handler writes `MatchStateSlice` via `SetMatchStatePayload`. The new state's `EnterCommands` fire (existing entry-side hook).

**Slice updates:**

- `TurnState`: rotates active player.
- `MatchStateSlice.Current`: `Player1TurnState` → `Player2TurnState`.
- `HistoryState`: `EndTurnAction` + `TransitionStateCommand` envelopes.

**System touches:** *Actions* (`EndTurnAction` handler) → *Engine FSM* (`Evaluate` returns new state-type) → *Engine* (`TransitionStateCommand` synthesis) → *State* (`MatchStateSlice` updated; `EnterCommands` fire) → *Presentation* (VM rebind via Registry).

> ⚠ **Open Q1.** OnExit hook on `MatchState`. Entry side is already covered by `EnterCommands` (fires when state is entered). The mirror — `OnExit(oldState)` for cleanup signals / cancellation of pending effects when leaving a state — is not in the locked design. Lean: yes, add `ExitCommands` (or equivalent) so authors don't re-implement the pattern.

---

## Phase 6: End match

Some action triggers a terminal condition (life total 0, deck empty, victory rule, etc.).

```mermaid
sequenceDiagram
  participant Engine
  participant Runtime
  participant Slices
  participant Presenter
  participant Registry

  Note over Engine: action fires that meets win/loss condition
  Engine->>Runtime: Evaluate(action, result)
  Runtime-->>Engine: EndOfMatchState(outcome) (terminal)
  Engine->>Engine: synthesize + Dispatch(TransitionStateCommand)
  Engine->>Slices: MatchStateSlice.Current = EndOfMatchState
  Engine->>Engine: Dispatch(EndMatchCommand(outcome))
  Engine->>Engine: engine.Session = EndedSession(outcome) (framework-internal field set)
  Engine-->>Presenter: MatchStateSlice → EndOfMatchState
  Presenter->>Registry: Create(EndOfMatchState)
  Registry-->>Presenter: EndOfMatchVM (final affordances: rematch, exit, view-history)
```

**Final state:**

- `engine.Session`: `EndedSession(MatchOutcome)`.
- `MatchStateSlice.Current`: `EndOfMatchState` (`ActionsOpen = false`, empty `Actions` → locked).
- `SessionGate` now rejects all gameplay commands.

**System touches:** *Engine FSM* (Evaluate returns terminal) → *Engine* (self-emits `EndMatchCommand`; flips session field) → *Presentation* (terminal VM).

> ⚠ **Open Q2.** Match-end initiator. Two paths: (a) `runtime.Evaluate` returns a terminal `MatchState` and the *host* listens and issues `EndMatchCommand`. (b) The engine itself issues `EndMatchCommand` after `Evaluate` returns terminal. (a) is host-driven; (b) is self-contained. Lean: (b) — the framework needs to work without a host orchestrator.

> ⚠ **Open Q3.** Rematch model. Is `EndedSession` terminal-only (engine instance is single-use) or can a `RestartMatchCommand` flip `EndedSession` back to `NotStartedSession`? Either is defensible. Lean: terminal — match instance is single-use; host disposes and recreates for rematch.

---

## Open questions summary

Three unresolved Qs after the walk so far. Grouped by surface:

| Group | Q | Topic | Status |
|---|---|---|---|
| Transitions | **Q1** | OnExit hook on `MatchState` | Lean: add `ExitCommands` or equivalent. |
| End match | **Q2** | Match-end initiator | Lean: engine self-emits `EndMatchCommand`. |
| End match | **Q3** | Rematch model | Lean: terminal; dispose. |

## What this spike confirms

The locked architecture handles every phase end-to-end **at a system-boundary level**. Engine orchestration (single drain, per-type routing on 4 `GameCommand` siblings with transition-loop prevention, `scope.Dispatch` for commands + `scope.Execute` for inline mutation clusters), validator chain authority, three-layer command execution (Effect → Handler → Mutator with fan-out), engine-invoked trigger fold across all four siblings, request system rendezvous through state with per-request cancellation policy + `Cancelled` flag on Response base + concurrent requests via `Task.WhenAll`, FSM evaluation (single `Evaluate` hook on `GameRulesRuntime`; ISBA-style game-specific checks live inside game `Evaluate` overrides), and per-state VM rebind via `scope.Subscribe<T>` all compose without contradiction.

The three remaining gaps are localized. Each is one of:

- **Missing hooks** — `OnExit` (Q1).
- **Ownership** — match-end initiator (Q2), rematch model (Q3).

None of the remaining gaps invalidate the architecture. All have defensible leans.

## Next moves

1. Walk Q1–Q3. Lock answers or punt with explicit deferrals.
2. Cascade settled answers into the relevant Models:
   - Q1 → engine Models (`engine/07-match-state.md` — exit hook).
   - Q2 / Q3 → engine Models (`engine/01-game-engine.md` — session lifecycle).
3. Re-do `support/samples/pile-counter.md` against the gap-filled architecture.
4. Refresh `PLANS/pile-counter-vertical-slice.md` vocabulary.
5. Execute the vertical slice.
