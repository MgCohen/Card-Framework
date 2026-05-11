---
type: adr
status: accepted
references: [features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, features/engine/models/09-state-based-actions.md, spikes/archive/fsm-architecture.md]
tags: [#adr, #engine, #runtime, #fsm, #match-state, #session-lifecycle]
---

# ADR-0009 — Engine as FSM; match state as Store slice; session lifecycle as engine field

Date: 2026-05-10 (drafted from `Product/spikes/archive/fsm-architecture.md` D1–D5 + D12 per the 2026-05-10 open-questions walk)
Context-docs: `Product/features/engine/models/01-game-engine.md`, `Product/features/engine/models/03-game-rules-runtime.md`, new Models `engine/07-match-state.md`, `engine/08-turn-flow.md`, `engine/09-state-based-actions.md`
Proposal: `Product/spikes/archive/fsm-architecture.md`

## Context

The engine's evaluation loop had quietly drifted toward "state evaluator" (tracked as the now-closed `oq-runtime-as-state-evaluator`):

- `runtime.Evaluate(action, result)` returned `IReadOnlyList<GameAction>` — a list of follow-up actions for the engine to enqueue.
- The runtime also wrote the available-actions snapshot through `IAvailableActions.Set` whenever it deemed appropriate.
- The match's phase / decision shape was carried indirectly as game-authored state slices (e.g., `TurnState`, `PhaseState`), with no framework-level shape for "what state is the match in?"
- Match lifecycle (`NotStarted` / `Started` / `Ended`) lived as an `EngineLifecycle` enum on the framework-owned `EngineState` slice, gated by `engine.Dispatch`.

Three problems compounded:

1. **No first-class concept of match state.** Phase transitions, decision pickers, end-of-turn resolution — all expressed by individual games as ad-hoc state slices read inside `Evaluate`. No shared vocabulary; presentation had to learn per-game which slice held the "current phase."
2. **Available-actions production was indirect.** Runtime → `IAvailableActions` service → mutator → `AvailableActionsState` slice → gateway / presentation. The snapshot existed so the gateway could reject illegal commands. If the runtime owned the match state directly, the legal-command set could live on that state and the service / slice / wire-form `ActionTemplate` machinery would dissolve.
3. **Session lifecycle is the wrong shape as a Store slice.** `EngineLifecycle.NotStarted/Started/Ended` is metadata *about* the engine — every networked game needs the same set (`Connecting`, `Reconnecting`, `Lobby`, `ShowingResults`). It must exist before any Store does (bootstrap chicken-and-egg). And no game rule reads it; it is framework infrastructure, not gameplay.

The 2026-05-10 open-questions walk on `Product/spikes/archive/fsm-architecture.md` settled the structural direction: the runtime is structurally a finite-state-machine evaluator, and the framework should adopt that shape directly.

## Options considered

- **A — Keep `Evaluate → IReadOnlyList<GameAction>`; introduce `MatchState` as an authoring convenience.** Rejected — the runtime is *already* an FSM in practice; pretending otherwise leaves it expressed indirectly through state slices and produces ad-hoc per-game shapes that presentation must learn.
- **B — Replace `Evaluate → list` with `Evaluate → MatchState`; transitions handled internally by the engine via direct slice writes.** Rejected — bypasses the command pipeline. Transitions wouldn't appear in history, triggers couldn't fire on them, and there would be a parallel write path for the canonical "current state of the match."
- **C — `Evaluate → MatchState`; engine dispatches `TransitionStateCommand` through the same pipeline as every other command. Session lifecycle becomes a typed sealed hierarchy on a `GameEngine` field (replaces the `EngineLifecycle` enum on a Store slice). Successor knowledge in `ITurnFlow`. Cross-cutting overrides as `ISBA` (state-based actions).** Chosen.

## Decision

Six structural locks (mapping to D1–D5 + D12 of the FSM-arch spike).

**1. The engine IS the FSM.** `GameRulesRuntime.Evaluate(action, result) → MatchState`. The engine compares the returned state's *type* to `MatchStateSlice.Current.GetType()`; if different, dispatches `TransitionStateCommand(newState)` through the pipeline. Same-type returns are no-ops. Type-equality (not reference-equality) matches intent — records are immutable so "stay" returns are usually `current`, but constructing a fresh same-typed instance shouldn't accidentally retrigger entry. Explicit re-enter semantics (`Reenter()`) deferred until forced by a real game.

**2. `MatchStateSlice` wraps a polymorphic `MatchState`.** Scaffold keys slices on `(Reference, typeof(TState))` with exact-type match — a polymorphic `MatchState` can't itself be a slice. The wrapper carries `Current`; the abstract record `MatchState : BaseState` carries three virtual members:
- `Actions` — `IReadOnlyList<Type>` of valid `GameCommand` subtypes in this state. Used by the engine's prophylactic intake gate (per the validator-pipeline shape in ADR-0010) and by tooling.
- `EnterCommands` — `IReadOnlyList<GameAction>` the engine enqueues when the state is entered (via `TransitionStateHandler`).
- `ShouldExit` — predicate `bool ShouldExit(GameAction, CommandResult, IStateScope)`. Pure; no mutations, no dispatch, no successor knowledge.

Concrete states carry phase-specific fields (`Attackers`, `HandCount`, etc.) directly on their record. Pattern-match `Current` to read them.

**3. State is data plus one predicate, not behavior.** Concrete `MatchState` subclasses carry phase-specific fields and override the three virtuals. No `OnEnter(ctx)`, no `Next(action) → MatchState`. Successor logic lives in `ITurnFlow` (D4); cross-cutting overrides live in `ISBA` (D5). The state is data the engine inspects; the predicate is the only behavior.

**4. Successor knowledge lives in `ITurnFlow.NextAfter`.** Single source of turn-structure truth:

```csharp
public interface ITurnFlow {
    MatchState NextAfter(MatchState current);
}
```

Games implement the interface to declare their turn shape. Adding a phase = new `MatchState` subclass + new arm in `NextAfter`. Tweaking turn order = edit `ITurnFlow` only; state classes don't change. The interface is visual-scripting-friendly (each arm is a transition-table entry with an optional guard).

**5. Cross-cutting state-driven overrides are SBAs (state-based actions).**

```csharp
public interface ISBA {
    MatchState? Check(IStateScope scope);   // null = no override
}
```

Examples: game-over check (a player reached zero life), legend rule (duplicate legendary permanents), cleanup phase (lethal damage removes creatures). The default `Evaluate` body runs registered SBAs in order before consulting `ShouldExit` / `Flow`; the first non-null wins.

```csharp
public override MatchState Evaluate(GameAction action, CommandResult result) {
    foreach (var sba in stateBasedActions)
        if (sba.Check(State) is { } over) return over;

    var current = State.Get<MatchStateSlice>().Current;
    return current.ShouldExit(action, result, State)
        ? Flow.NextAfter(current)
        : current;
}
```

Games override `Evaluate` only when they need custom orchestration; the default body is the composition `SBAs → ShouldExit → Flow.NextAfter`.

**6. Session lifecycle is a typed sealed hierarchy on `GameEngine.Session`, not a Store slice.**

```csharp
public abstract record SessionState;
public sealed   record NotStartedSession                       : SessionState;
public sealed   record InMatchSession                          : SessionState;
public sealed   record EndedSession(MatchOutcome Outcome)      : SessionState;
// Future additions: ConnectingSession, ReconnectingSession, ShowingResultsSession, ...

public sealed class GameEngine {
    public SessionState Session { get; private set; } = new NotStartedSession();
    public event Action<SessionState>? SessionChanged;
    // ...
}
```

Replaces / subsumes the `EngineLifecycle` enum and the `Lifecycle` field on `EngineState`. Match-FSM subscribers continue using `MatchStateSlice` from the Store; session subscribers use the engine event. Two observation patterns, honest about the layering split.

`engine.StartMatch()` transitions `NotStartedSession → InMatchSession` and seeds `MatchStateSlice.Current` with the initial state; the first state's `EnterCommands` fire on the next drain. `engine.FinishMatch(outcome)` force-transitions match to a `GameOverState(outcome)` and session to `EndedSession(outcome)`. `engine.Dispose()` synthesizes `MatchOutcome.Aborted("Disposed")` if not already ended.

**Layering consequence.** The engine's intake gate now consults two distinct things: `engine.Session` (is the engine accepting commands?) and `MatchStateSlice.Current.Actions` (is this command type valid in the current match state?). Both checks live as registered framework validators (`SessionGate`, `MatchStateActionGate`) once the validator/handler split lands in ADR-0010. The hardcoded engine-side lifecycle gate retires at that point.

**Hybrid future flagged.** Long-term shape likely **B+C hybrid** — host code (Unity scene, dedicated server) owns session orchestration (lobby, matchmaking, reconnect); engine exposes hooks (`Session` property, `SessionChanged` event, `StartMatch` / `FinishMatch` / `Dispose`). Framework provides typed states and hooks; host wires the orchestration appropriate to its environment.

## Consequences

- **Enables** a uniform, framework-level concept of match state that presentation can subscribe to (`MatchStateSlice`), tooling can introspect (`Actions: IReadOnlyList<Type>`), and games extend by adding new `MatchState` subclasses. Phase / decision / cleanup states are all `MatchState`s — same shape, same channel.
- **Enables** per-state polymorphic ViewModels (ADR-0012's territory) — `MatchStateViewModel<TState>` binds to the current state; `ViewModelRegistry` maps state type → factory. Presentation no longer projects a global `AffordanceMap` from a generic snapshot.
- **Enables** transitions as auditable commands. `TransitionStateCommand` enters the history envelope like everything else; `OnAfter<TransitionStateCommand>` triggers fire naturally; replay reconstructs exactly the same state sequence.
- **Enables** the layered intake gate model. Session-level gating and match-state-level gating compose through the validator pipeline (per ADR-0010) instead of through bespoke engine logic.
- **Retires** the `IAvailableActions` service and the `AvailableActionsState` slice. `MatchState.Actions: IReadOnlyList<Type>` is the new framework shape. The corresponding canonical Model (`Product/features/engine/models/04-available-actions.md`) is archived. The wire-form `ActionTemplate` (`Product/features/actions/models/05-available-actions-snapshot.md`) is retired in ADR-0012 (presentation per-state VMs).
- **Retires** the `EngineLifecycle` enum and the `Lifecycle` field on `EngineState`. The intake-gate check moves from a hardcoded engine block to a registered framework validator (`SessionGate`) consulting `engine.Session` once ADR-0010 lands. Until then, `engine.Dispatch` keeps a transitional inline gate against `engine.Session`.
- **Constrains** every match-state read to go through `MatchStateSlice` — games can't carry the current phase in a private game-authored slice and skip the framework shape. (They *can* still carry phase-specific *data* in separate slices, but the "what state are we in?" answer is structurally on `MatchStateSlice.Current`.)
- **Constrains** the runtime's `Evaluate` to return a single `MatchState`, not a list. Multi-action follow-ups now flow through `MatchState.EnterCommands` (enqueued by `TransitionStateHandler`) or through commands dispatched by handlers / effects (the latter through the effect pipeline shape in ADR-0011).
- **Reverts / replaces** the parts of [ADR-0006](decisions/0006-two-pipeline-architecture.md) that described drain-end logic as "the runtime detects 'queue is about to empty' by observing state plus its own follow-up emptiness." With `Evaluate → MatchState`, that detection is structural: the FSM transitions or stays. ADR-0006's two-pipeline principle (framework command pipeline + game effect pipeline) is unchanged.
- **Defers** `MatchState` authoring ergonomics — DSL, record builders, visual-scripting bindings — to the authoring feature. Games author concrete `MatchState` subclasses + an `ITurnFlow` implementation + an `IReadOnlyList<ISBA>` by hand for now.
- **Defers** SBA registration ergonomics. `IReadOnlyList<ISBA>` on the runtime base is sufficient; richer composition (priority, enable/disable, per-state suppression) waits for a real game.
- **Defers** explicit `Reenter()` semantics. Same-type returns from `Evaluate` are no-ops today. Games that need re-entry on same-typed state changes will force the question.

## Notes

- **Spike status.** `Product/spikes/archive/fsm-architecture.md` is the historical provenance source for this ADR (and ADRs 0010–0012). All four sub-ADRs and their Model cascades have landed; the spike was archived in the post-cascade consolidation PR (2026-05-11).
- **Sub-ADRs in the cascade.** ADR-0010 (validator / handler split), ADR-0011 (effect orchestration + dispatch + requests), ADR-0012 (presentation per-state ViewModels) each cascade their own piece of the FSM-arch spike. This ADR (0009) is structurally independent of 0010 and 0011; ADR-0012 depends on this one (per-state VMs reference `MatchState` shape).
- **Hybrid session lifecycle.** The B+C hybrid is flagged but not pinned: framework provides typed `SessionState` + the `SessionChanged` event; host wires the orchestration appropriate to its environment. Pin when the first networked / multi-match host forces the question.
- **Transition-handler shape.** `TransitionStateHandler` is framework-internal:
  ```csharp
  public sealed class TransitionStateHandler : GameCommandHandler<TransitionStateCommand, TransitionResult> {
      public override Task<TransitionResult> Handle(TransitionStateCommand cmd) {
          Store.Execute(new SetMatchStatePayload(cmd.NewState));   // mutate slice
          foreach (var enter in cmd.NewState.EnterCommands)
              Engine.Enqueue(enter);                                // chained via engine
          return Task.FromResult(new TransitionResult());
      }
  }
  ```
  The base-class injection pattern shown (`Store`, `Engine` via property injection) is the shape ADR-0010 will land for all handlers. Until then, the engine wires `TransitionStateHandler` via the existing `ICommandHandler<TCommand, TResult>` shape with constructor-injected `Store` and an `IEngineQueue` capability.
