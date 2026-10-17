---
type: spike
status: active
supersedes: Product/spikes/match-state-redesign.md
references:
  - Product/spikes/framework-architecture.md
  - Product/spikes/request-service.md
  - Product/decisions/0009-engine-as-fsm.md
sandbox: Assets/Spike/ECS/StateMachine/
tags: [#spike, #state, #fsm, #state-service, #service-first]
---

# State Service — game-state FSM on a service-first substrate

> **Update 2026-10-18 (phase 0 housekeeping).** The wire shape changed after 5b. PR #165 replaced the generic `StatePushEvent<TState>` with one non-generic `StatePushEvent { [Packed] int Slot }`: `GameState` is now a `TypeKey` record keyed by slot, `GameStateRegistry` (a `SlotRegistry<GameState>`) maps slots to states, and one `StatePushHandler` looks the state up by slot. PR #180 renamed `StateInstaller` to `StateInstaller`. The shapes below are updated to match the code; the § Landed log keeps the original 5b record. The decision is recorded in [ADR-0014](Product/decisions/0014-service-first-state-model.md).

> **Status: active (step 5b cursor skeleton shipped 2026-05-31, commit `92bee77`).** Defines the state-specific shape of the framework's FSM layer, sitting on top of the substrate locked in [`framework-architecture.md`](framework-architecture.md). The 5b build (`Framework.States`) ships the Store + Service + per-type apply handler cursor; the FSM behavior surface defers to 5c (see § Step 5b build scope and § Landed). Supersedes [`match-state-redesign.md`](match-state-redesign.md), which walked the six-sketch design evolution (v1 polymorphic slice → v6 stateless records) and surfaced the framework-foundational questions that are now answered separately. This doc is short on purpose — the substrate questions live in framework-arch; what remains here is just the state pieces.
>
> **Structural twin of [`request-service.md`](request-service.md).** State is the sister service to Requests (step 5a): same Store + Service + per-type apply handler pattern on the same wire substrate. Read that doc's "Locked decisions" (L1–L6) for the substrate conventions this one inherits; this doc only states the state-specific deltas.

## TL;DR

**State is a typed identity, not a data container.** Each FSM state is a zero-data record subclass of `GameState`. The current cursor lives on `StateStore` (both sides, reconstructable purely from the wire); the server-only `StateService.Push<TState>()` emits a generic `StatePushEvent<TState>` whose **type parameter is the payload** (one network channel per state, mirroring `TagSetEvent<T>` — no byte/registry encoding); the framework's `StatePushHandler<TState>` is the sole writer of `Current` on both sides. Transitions are wire events, not a parallel path. All data that would have lived on a state lives on entities (per-card tags) or `MatchService` (turn, active player, outcome).

The behavior surface that makes states *do* things — `Actions`, `OnEnter`, `OnExit`, `IsCompleted`, the engine `Evaluate` hook, `IStateFlow`, `PushStateCommand` — lands in **step 5c** alongside the command/effect pipeline those pieces depend on. Step 5b ships the cursor skeleton (see below).

## Step 5b build scope — what ships now vs 5c

Step 5b mirrors exactly what step 5a (Requests) shipped: **wire event + store + service + apply handler + layer + engine handle, no command pipeline.** The FSM behavior surface defers to 5c because `GameAction`, `IStateScope`/`ICommandScope`, and the engine `Evaluate` loop don't exist in `Framework/` yet — minting placeholders for them now would pre-name shapes 5c owns for real.

**Ships in 5b (`Framework.States`):**

| Piece | Shape |
|---|---|
| `GameState` | abstract record `GameState(int Slot) : TypeKey(Slot)` — no data fields; identity is the type and its slot |
| baseline states | `MatchNotStartedState`, `PlayingState`, `MatchEndedState` |
| `StatePushEvent` | wire event with one `[Packed] int Slot` — the slot of the new state *(was generic `StatePushEvent<TState>` in 5b; changed by #165)* |
| `StateStore` | holds `Current` (both sides); initialized to `MatchNotStartedState` |
| `StateService` | **server-only**; `Push<TState>()` emits `new StatePushEvent { Slot = new TState().Slot }` |
| `StatePushHandler` | framework-shipped sole writer; `Apply` sets `Current` to `GameStateRegistry.BySlot(ev.Slot)`; registered both sides |
| `GameStateRegistry` | `SlotRegistry<GameState>`; `WithBaselines()` registers the three baseline states; games register their own |
| `StateInstaller` | role-gated; store, registry and handler both sides, service server-only |
| `GameEngine.States` / `GameEngine.StateStore` | server-only discoverable handles |

**Defers to 5c (needs the command/effect pipeline):**

- `GameState` virtuals — `Actions`, `OnEnter(scope)`, `OnExit(scope)`, `IsCompleted(action, result, scope)`.
- `IStateScope` — the narrow read view passed to those virtuals.
- `IStateFlow.NextAfter` — successor map; zero-dependency to *define*, but its only consumer is the `Evaluate` hook.
- `PushStateCommand` + `PushStateHandler` (command) — routing a transition *through* the dispatch path. In 5b, `StateService.Push` emits the wire event directly (mirrors `RequestService.Open`).
- Engine `Evaluate` hook — auto-advance after each command resolves.
- **Zero-input deadlock** (Open items) — can't occur until there's an auto-advance loop to deadlock; the three-option pick gets made in 5c with the real scope types in hand.

The locked fork picks for 5b: **(1)** skeleton-only scope; **(2)** zero-member `GameState`; **(3)** defer `IStateFlow`; **(4)** initial `Current = MatchNotStartedState`.

**Wire encoding — typed channels, not a byte registry.** *(5b history. PR #165 went back to one `StatePushEvent` carrying an `int Slot`, with `GameStateRegistry` as the slot map — the `TypeKey`/`SlotRegistry` pattern shared with tags and entity types.)* An earlier draft of this doc carried a single `StatePushEvent` with a `[Packed] byte NewKind/PrevKind` plus an `IStateRegistry` (byte ↔ Type, `Func<T>` factories). That's the pre-typed-wire shape. The framework already discriminates wire events by *type* via per-closed-generic network channels (`TagSetEvent<int>` vs `<bool>`), so state uses the same mechanism: `StatePushEvent<TState>` per state, the closed type *is* the discriminator. No bytes, no registry, no manual kind assignment. This depends on AutoPacker emitting a valid empty `Packed` for a concrete zero-`[Packed]`-field leaf (shipped in `com.scaffold.autopacker` `15f5e5d`, retiring the `byte Reserved` workaround).

## Why this exists

The framework needs an FSM concept to express turn structure and game phases. The earlier design (ADR-0009: slice-based `MatchState`) was anchored to a Scaffold store substrate that the vertical-slice rebuild retired. The match-state-redesign spike walked six sketches and landed on stateless records — and in the process surfaced framework-wide questions (where data lives, how mutation/wire/rollback relate) that have since been locked in [`framework-architecture.md`](framework-architecture.md).

This spike sits on top of that substrate and documents only what's state-specific.

## Reading guide

Short doc. Read top to bottom. If you haven't read [`framework-architecture.md`](framework-architecture.md), read that first — it defines `IProjection<T>`, the watch API, the server/client asymmetry, and the `entity.Set` decomposition referenced throughout.

---

## Vocabulary

- **game-state** = the overall situation of the game (entities, services, "what is happening"). Not the subject of this spike.
- **state** = a position in the game's FSM ("Main Phase," "Combat," "Mulligan"). What counts as a state is game-defined. **This is the subject.**

---

## The shape

> **Step 5b shape.** The blocks below marked **[5b]** ship now. Blocks marked **[5c]** are the FSM behavior surface, reproduced here for the full picture but deferred per § Step 5b build scope.

### `GameState` — zero-data record **[5b]**

```csharp
public abstract record GameState;

public sealed record MatchNotStartedState : GameState;
public sealed record PlayingState         : GameState;
public sealed record MatchEndedState      : GameState;
```

Identity is the type. Same-kind comparison is `is`; structural equality is trivially true (zero fields). No mutable fields, no constructor parameters, no wire payload — the record carries nothing. The virtual behavior surface (`Actions`/`OnEnter`/`OnExit`/`IsCompleted`) is added in **5c** when `GameAction`/`IStateScope` exist:

```csharp
// [5c] — added once the command/effect pipeline lands
public abstract record GameState
{
    public virtual IReadOnlyList<Type>       Actions => [];
    public virtual IReadOnlyList<GameAction> OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction> OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) => false;
}
```

### `StateStore` — owner of the cursor **[5b]**

Post-step-4, the cursor lives on a **both-sides store**, not on the service. The store is reconstructable purely from the wire; the per-type apply handler is its sole writer (the L12 contract from the service-pattern reshape).

```csharp
public sealed class StateStore
{
    public GameState Current { get; private set; } = new MatchNotStartedState();

    internal void Set(GameState state) => Current = state;
}
```

### `StatePushEvent` — transition event **[5b, reshaped by #165]**

One wire event for every state. The payload is the slot of the new state; `GameState` is a `TypeKey` record, so each state type has a fixed slot.

```csharp
public partial class StatePushEvent : WireEvent
{
    [Packed] public int Slot;
}
```

### `StateService` — the Push surface **[5b]**

Server-only, mirrors `RequestService`. `Push` doesn't write `Current`; it emits the event and the handler does the write (same on both sides).

```csharp
public sealed class StateService
{
    readonly IEventEmitter _events;

    public StateService(IEventEmitter events) { _events = events; }

    public void Push<TState>() where TState : GameState, new()
        => _events.Emit(new StatePushEvent { Slot = new TState().Slot });
}
```

### `StatePushHandler` — sole writer **[5b, reshaped by #165]**

One framework-shipped handler, registered both sides by `StateInstaller`. Same `Apply` runs server-side (after `Push` emits) and client-side (after wire delivery). It resolves the state from the slot through `GameStateRegistry`.

```csharp
internal sealed class StatePushHandler : WireEventHandler<StatePushEvent>
{
    readonly StateStore _store;
    readonly GameStateRegistry _registry;

    public StatePushHandler(StateStore store, GameStateRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    public override void Apply(StatePushEvent ev) => _store.Set(_registry.BySlot(ev.Slot));
}
```

Games add states by registering them in `GameStateRegistry` (`registry.Register<MyState>()`); slots must be unique.

### `IStateFlow` — successor map **[5c]**

```csharp
public interface IStateFlow
{
    GameState? NextAfter(GameState current, IStateScope scope);
}
```

Game-defined. Replaces ADR-0009's `ITurnFlow`. Returns `null` for terminal states. Deferred to 5c — zero-dependency to define, but its only consumer is the `Evaluate` hook, so it ships with that hook.

### `IStateScope` — narrow read view for virtual methods **[5c]**

```csharp
public interface IStateScope
{
    IEntityReader Entities { get; }
    IMatchReader  Match    { get; }
    GameState     Current  { get; }
}
```

Predicates (`IsCompleted`) and generators (`OnEnter`/`OnExit`) don't mutate state directly — they return commands, which the handler enqueues. The scope is read-only by design.

### `PushStateCommand` + `PushStateHandler` **[5c]**

> Deferred to 5c. In 5b, `StateService.Push` emits `StatePushEvent` directly (mirrors `RequestService.Open` emitting the Request — no command pipeline). The command-routed form below replaces that direct emit once the dispatch path and `OnEnter`/`OnExit` generators exist.

```csharp
public sealed record PushStateCommand(GameState NewState) : GameAction;
public sealed record PushStateResult                       : CommandResult;

public sealed class PushStateHandler : ICommandHandler<PushStateCommand, PushStateResult>
{
    public PushStateResult Handle(PushStateCommand cmd, ICommandScope scope)
    {
        var prev = _state.Current;
        var next = cmd.NewState;

        var prevScope = new StateScope(_entities, _match, prev);
        foreach (var c in prev.OnExit(prevScope))
            scope.Enqueue(c);

        // single mutator path: stream.Append dispatches Apply, which writes Current
        _stream.Append(new StatePushEvent(
            newKind:  _registry.KindOf(next),
            prevKind: _registry.KindOf(prev)));

        var nextScope = new StateScope(_entities, _match, next);
        foreach (var c in next.OnEnter(nextScope))
            scope.Enqueue(c);

        return new PushStateResult();
    }
}
```

The handler never writes `_state.Current` directly. It computes the event, hands it to the stream, and lets the apply handler mutate. This is the F4 contract from framework-arch — and in 5c the transition still rides `StatePushEvent<TState>` (the wire form locked in § The shape), just emitted from inside `PushStateHandler` instead of `StateService.Push`.

### Engine's `Evaluate` hook **[5c]**

After every command resolves, the engine asks the current state whether to advance:

```csharp
private void Evaluate(GameAction action, CommandResult result)
{
    if (action is PushStateCommand) return;          // don't recurse on push outputs
    var current = _state.Current;
    if (!current.IsCompleted(action, result, _stateScope)) return;
    var next = _flow.NextAfter(current, _stateScope);
    if (next != null) Enqueue(new PushStateCommand(next));
}
```

`PushStateCommand` is itself a command — transitions are not a parallel path. Same pipeline; same wire-block structure; same handler shape.

---

## Push lifecycle **[5c]**

> The OnExit → Append → OnEnter generator lifecycle below is the 5c shape. In 5b the lifecycle is just: `StateService.Push` → emit `StatePushEvent` → `StatePushHandler.Apply` sets `StateStore.Current`. No generators.

```
   handler receives PushStateCommand
        │
        ├─► prev.OnExit(scope)           → enqueue generated commands
        │
        ├─► emit StatePushEvent (slot of TNext)
        │      └─ dispatches StatePushHandler.Apply
        │           └─ store.Current = new TNext()
        │
        ├─► next.OnEnter(scope)          → enqueue generated commands
        │
        └─► return PushStateResult
```

OnExit/OnEnter outputs flow through the normal command pipeline. They are **generators** — their returned commands produce their own wire events. Replay walks those events; the hooks never re-fire (Q6 from match-state-redesign; matches framework-arch's general rule that wire events are the source of truth, hooks are not).

---

## Baseline states

```csharp
public sealed record MatchNotStartedState : GameState;
public sealed record PlayingState         : GameState;
public sealed record MatchEndedState      : GameState;

// StateInstaller registers GameStateRegistry.WithBaselines() on both sides; games add their own.
registry.Register<MatchNotStartedState>();
registry.Register<PlayingState>();
registry.Register<MatchEndedState>();
```

A simplest game (e.g., Pile Counter) uses these three as-is: `NotStarted → Playing → Ended`. A phase-structured game (MTG / Hearthstone flavor) authors additional `GameState` records and one `RegisterStateType<TState>()` call each.

---

## Worked example — simplified MTG flow

The stateless-records payoff: data that would have lived on the state record lives on entities or `MatchService` instead.

```csharp
public sealed record UpkeepState : GameState
{
    public override IReadOnlyList<Type> Actions => [typeof(PassCommand)];

    public override IReadOnlyList<GameAction> OnEnter(IStateScope s) =>
    [
        new UntapAllCommand(),
        new DrawCardCommand(s.Match.ActivePlayer),
    ];

    public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
        a is PassCommand;
}

public sealed record DeclareAttackersState : GameState
{
    public override IReadOnlyList<Type> Actions =>
        [typeof(ToggleAttackerCommand), typeof(CommitAttackersCommand)];

    public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
        a is CommitAttackersCommand;
}

public sealed record CombatDamageState : GameState
{
    // pure resolution; no player input
    public override IReadOnlyList<Type> Actions => [];

    public override IReadOnlyList<GameAction> OnEnter(IStateScope s)
    {
        // "which creatures are attacking?" → entity query, not record field
        var cmds = new List<GameAction>();
        foreach (var atk in s.Entities.With(MtgTags.IsAttacking))
            cmds.Add(new DealDamageCommand(atk.Id, atk.Get(MtgTags.Power)));
        return cmds;
    }

    public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s)
    {
        foreach (var atk in s.Entities.With(MtgTags.IsAttacking))
            if (!atk.Has(MtgTags.DamageResolved)) return false;
        return true;
    }
}
```

`DeclareAttackersState` doesn't carry an `ImmutableList<Ref<Card>> Selected`. The selection lives as the `IsAttacking` tag on each creature; the predicate queries the world. This is the structural payoff — re-pushing the state would give the same answer because the answer doesn't live on the record.

Flow:

```csharp
public sealed class SimplifiedMtgFlow : IStateFlow
{
    public GameState? NextAfter(GameState current, IStateScope s) => current switch
    {
        MatchNotStartedState  => new UpkeepState(),
        UpkeepState           => new Main1State(),
        Main1State            => new DeclareAttackersState(),
        DeclareAttackersState => new DeclareBlockersState(),
        DeclareBlockersState  => new CombatDamageState(),
        CombatDamageState     => new Main2State(),
        Main2State            => new EndState(),
        EndState              => new UpkeepState(),    // next turn
        MatchEndedState       => null,
        _                     => null,
    };
}
```

The full sandbox sits at `Assets/Spike/ECS/StateMachine/`; tests pin the stateless-state contract via reflection and the worked-example behavior.

---

## Sample wire stream — one combat transition

```
... player ends Main1 ...
[Block: Pass]                                ← command frame
[StatePushEvent: prev=11(Main1), new=12(DeclareAttackers)]   ← 2 bytes

... player marks two attackers, commits ...
[Block: ToggleAttacker source=Creature_A]
  [TagSet: Creature_A.IsAttacking = true]
[Block: ToggleAttacker source=Creature_B]
  [TagSet: Creature_B.IsAttacking = true]
[Block: CommitAttackers]
[StatePushEvent: prev=12(DeclareAttackers), new=13(DeclareBlockers)]

... defender blocks, commits ...
[Block: AssignBlocker]
  [TagSet: Creature_X.Blocking = Creature_A]
[Block: CommitBlockers]
[StatePushEvent: prev=13(DeclareBlockers), new=14(CombatDamage)]

... damage resolves via OnEnter ...
[Block: DealDamage source=Creature_A target=Creature_X]
  [TagChange: Creature_X.Health 3 → 1]
  [TagSet: Creature_A.DamageResolved = true]
[Block: DealDamage source=Creature_B target=Player2]
  [TagChange: Player2.Life 20 → 17]
  [TagSet: Creature_B.DamageResolved = true]
[StatePushEvent: prev=14(CombatDamage), new=15(Main2)]
```

Every wire event is a known small concrete class. State pushes are 2 bytes each. Everything else uses the existing wire alphabet (TagSet / TagChange / Block).

---

## Where displaced data lives

| Used in the MTG sketch | Lives on |
|---|---|
| `ActivePlayer(scope)`, `DefendingPlayer(scope)` | `MatchService` |
| `Match.Turn` | `MatchService` |
| `Card.IsAttacking`, `Card.Blocking`, `Card.DamageResolved` | the Card entity |
| Outcome (winner / draw) | `MatchService` |

`MatchService` is the framework-arch F1 service that holds game-wide singletons. It projects from `TurnIncrementEvent`, `ActivePlayerChangedEvent`, `OutcomeSetEvent`, etc. Same pattern as `StateService`; same `IProjection<T>` contract.

---

## Relation to ADR-0009 (engine-as-FSM)

- **D1** *Engine IS the FSM* — survives. Engine's `Evaluate` hook decides successors; runtime returns `GameState?`.
- **D2** *`MatchStateSlice` wraps polymorphic `MatchState`* — retired by framework-arch F1. Cursor lives on `IStateService`; wire encoding is `StatePushEvent` (2 bytes, kind-keyed).
- **D3** *State is data + predicate* — survives in form but with zero data fields. Predicates stay; data moves to entities or `MatchService`.
- **D4** *Successor knowledge in `ITurnFlow.NextAfter`* — survives, renamed `IStateFlow.NextAfter`.
- **D5** *ISBA cross-cutting* — already retired (2026-05-11 framework-wide).
- **D12** *Session lifecycle as engine field* — survives unchanged. `SessionState` typed hierarchy on `engine.Session` is independent of the match-state FSM.

---

## Open items (state-specific only)

- **Zero-input deadlock. [pick deferred to 5c]** A pure-resolution state (no `Actions`, OnEnter generates no commands when the world is empty) can deadlock — `IsCompleted` only fires after a command, but no command arrives. Pinned in `StatelessStateProofTests.CombatDamage_WithZeroAttackers_AutoCompletes`. Three viable fixes:
  - Re-evaluate after the push lands (vacuous completion advances flow).
  - Contract: pure-resolution states must emit ≥1 command in OnEnter.
  - Auto-emit a `NoOpCommand` when OnEnter returns empty.

  **The deadlock can't occur until the `Evaluate` auto-advance loop exists (5c)** — there's nothing to stall in the 5b cursor skeleton. The pick gets made in 5c with the real `IStateScope`/`GameAction`/`Evaluate` types in hand, rather than guessed now.

- **Match-end condition path.** `IStateFlow.NextAfter(MatchEndedState)` returns null; no game-over check sits in the flow itself. The framework needs an auto-emit pattern for game-over detection — likely a `MatchService` observer or an `EndMatchCommand` triggered when `Outcome != Undecided`. Out of scope for `IStateService`.

- **State-kind ID assignment.** Manual byte registration today. `[StateKind(N)]` attribute auto-discovered at bootstrap is the natural next step. Auto-allocation (assign based on registration order) is risky for cross-version replay; rejected.

- **State behavior placement.** Currently on the record itself (virtual methods). Alternative: per-kind handler classes (`IStateBehavior<TKind>`) registered against the kind. Cosmetic switch; no wire impact. Defer until ergonomics force it.

- **Block / causality threading on the wire.** When `PushStateCommand` runs, the `StatePushEvent` lands as a child block of the command's block. OnEnter-generated commands open *sibling* blocks at the next level. Confirm placement when the wire-block model from framework-arch lands in concrete form.

- **Hierarchy / pushdown / parallel regions.** Flat FSM for now. Tag-grouping handles "this state is combat-flavored" without hierarchy. Pushdown (push/pop interrupts for menus, modal decisions) deferred until a game forces it. Parallel regions stay as per-actor tag conventions (e.g., mulligan tags per Player) rather than parallel state machines.

- **Channel cardinality.** No per-state byte kind anymore — each state is a network channel. The cap is the Wire-level channel-id space (`Network._nextId` is a `byte`), shared across *all* wire event types (tags + entities + requests + states). Phase-flavored games add ~15 states; nowhere near the shared 256 ceiling. If the total ever approaches it, widening the channel id is a Wire-layer change, not a state concern.

---

## Patch cascade

Lands together with framework-arch's cascade:

1. **New ADR (after the framework-arch ADR)** — locks F1 application to FSM: `IStateService`, `PushStateCommand`, `StatePushEvent`, baseline records.
2. **Refresh — `features/engine/archive/07-match-state.md`** → `GameState` shape; drop slice wrapper; rename `MatchState` → `GameState`; update virtual signatures.
3. **Refresh — `features/engine/archive/08-turn-flow.md`** — `ITurnFlow` → `IStateFlow`; returns `GameState?`.
4. **Refresh — `features/engine/archive/03-game-rules-runtime.md`** — `Evaluate` returns `GameState?`; remove slice references.
5. **Refresh — ADR-0012 + `features/presentation/archive/01-match-state-view-model.md`** — dispatch on `GameState` record type, not slice polymorphism.
6. **Sweep — `features/state/archive/state.md`** — heavy rewrite or absorb into new feature folder.
7. **Glossary sweep** — retire `MatchState`, `MatchStateSlice`, `TransitionStateCommand`; add `GameState`, `StateStore`, `StateService`, `StatePushEvent`, `IStateFlow` (5c), `PushStateCommand` (5c). (No `IStateRegistry` — typed channels replaced it.)
8. **Sandbox refactor** — `MatchSingleton` on the spike's `World` becomes `MatchService` per framework-arch F1.

---

## Notes

- The earlier six-sketch walk (v1 polymorphic slice → v6 stateless records) lives in [`match-state-redesign.md`](match-state-redesign.md), now superseded. The historical evolution is preserved there for archeology — readers who want to see *why* v1–v5 were rejected can read that doc.
- The sandbox at `Assets/Spike/ECS/StateMachine/` is the original prototype. It carries `MatchSingleton` on a spike-local `World` (pre-framework-arch shape); the graduated implementation now lives in `Assets/Framework/States/` (see § Landed), and the spike sandbox is superseded.

---

## Landed (2026-05-31, commit `92bee77`)

Step 5b cursor skeleton shipped to `Framework.States`, structurally mirroring `Framework.Requests`:

1. ✅ `Framework.States` asmdef (Runtime + Tests).
2. ✅ `GameState` zero-data abstract record + `MatchNotStartedState` / `PlayingState` / `MatchEndedState` baselines.
3. ✅ `StatePushEvent<TState>` generic wire event (zero `[Packed]` fields — type param is the payload, one channel per closed state; mirrors `TagSetEvent<T>`). Depends on the AutoPacker empty-`Packed` codegen, landed in `com.scaffold.autopacker` `15f5e5d` (commit `0b178a0` bumps the lock).
4. ✅ `StateStore` (`Current`, both sides, init `MatchNotStartedState`) / `StateService` (server-only `Push<TState>()`) / `StatePushHandler<TState>` (sole writer, both sides).
5. ✅ `RegisterStateType<TState>` DI sugar *(later replaced by `GameStateRegistry`, #165)* + `StateInstaller` role-gating (store + handlers both sides, service server-only) + `FrameworkAppFlowRoot` registration. `GameEngine` grows `States` + `StateStore` (server-only handles).
6. ✅ Test fixture — `StateServiceTests` (5 scenarios): store starts on `MatchNotStarted`; push mirrors `Current` to both sides; sequential pushes advance both cursors; re-push stays converged; each state type is a distinct channel (also proves the empty-`Packed` codegen emitted). `GameplayLayerTests` extended: server resolves `States` / `StateStore`; client has `StateStore` but `StateService` throws.

**353/353 EditMode tests pass** (+5 from `StateServiceTests`; +0 net from the extended `GameplayLayerTests` assertions).

The build matched this spec's 5b scope without shape surprises. Everything under **[5c]** (FSM behavior surface, `IStateFlow`, `Evaluate` hook, `PushStateCommand`, zero-input-deadlock pick) remains deferred to the command/effect pipeline step. The heavier doc cascade (new ADR, `engine/07-match-state.md` rewrite, Models 01/03/08 refresh, GLOSSARY/OPEN-ITEMS sweep) is tracked in SESSION-CHECKPOINT and may land across follow-up sessions.

- No further sandbox edits; the spike prototype is superseded by `Framework.States`.
