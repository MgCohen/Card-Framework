---
type: spike
status: accepted
supersedes: Product/spikes/match-state-redesign.md
references:
  - Product/spikes/framework-architecture.md
  - Product/decisions/0009-engine-as-fsm.md
sandbox: Assets/Spike/ECS/StateMachine/
tags: [#spike, #state, #fsm, #state-service, #service-first]
---

# State Service — game-state FSM on a service-first substrate

> **Status: accepted (decisions locked, patch cascade pending).** Defines the state-specific shape of the framework's FSM layer, sitting on top of the substrate locked in [`framework-architecture.md`](framework-architecture.md). Supersedes [`match-state-redesign.md`](match-state-redesign.md), which walked the six-sketch design evolution (v1 polymorphic slice → v6 stateless records) and surfaced the framework-foundational questions that are now answered separately. This doc is short on purpose — the substrate questions live in framework-arch; what remains here is just the state pieces.

## TL;DR

**State is a typed identity, not a data container.** Each FSM state is a zero-data record subclass of `GameState` with virtual methods (`Actions`, `OnEnter`, `OnExit`, `IsCompleted`). An `IStateService` owns the current state and projects from `StatePushEvent`. Transitions flow through `PushStateCommand`. The wire emits a single 2-byte `StatePushEvent` per transition. All data that would have lived on a state lives on entities (per-card tags) or `MatchService` (turn, active player, outcome).

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

### `GameState` — zero-data record

```csharp
public abstract record GameState
{
    public virtual IReadOnlyList<Type>        Actions   => [];
    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) => false;
}
```

Identity is the type. Same-kind comparison is `is`; same-instance equality is structural and always true (zero fields). No mutable fields. No constructor parameters. No payload on the wire because the record carries no payload.

### `IStateService` — owner of the cursor

```csharp
public interface IStateService
{
    GameState Current { get; }
    void Push(GameState next);   // sugar — constructs + dispatches PushStateCommand
}

public sealed class StateService : IStateService, IProjection<StatePushEvent>
{
    public GameState Current { get; private set; }

    public void Push(GameState next) => _engine.Enqueue(new PushStateCommand(next));

    public void Apply  (StatePushEvent ev) => Current = _registry.Resolve(ev.NewKind);
    public void Unapply(StatePushEvent ev) => Current = _registry.Resolve(ev.PrevKind);
}
```

`Apply`/`Unapply` are the only path that mutates `Current` (per framework-arch F4). Same code runs on the server (via the handler's `stream.Append` dispatch) and on the client (via wire arrival).

### `IStateRegistry` — byte ↔ Type

```csharp
public interface IStateRegistry
{
    void      Register<T>(byte kind, Func<T> factory) where T : GameState;
    GameState Resolve(byte kind);
    byte      KindOf(GameState state);
}
```

Manual byte assignment for replay stability across versions. Framework reserves kinds **0–9**; game-authored kinds start at **10**. A `[StateKind(N)]` attribute or source-gen approach is a later ergonomics improvement.

### `IStateFlow` — successor map

```csharp
public interface IStateFlow
{
    GameState? NextAfter(GameState current, IStateScope scope);
}
```

Game-defined. Replaces ADR-0009's `ITurnFlow`. Returns `null` for terminal states.

### `IStateScope` — narrow read view for virtual methods

```csharp
public interface IStateScope
{
    IEntityReader Entities { get; }
    IMatchReader  Match    { get; }
    GameState     Current  { get; }
}
```

Predicates (`IsCompleted`) and generators (`OnEnter`/`OnExit`) don't mutate state directly — they return commands, which the handler enqueues. The scope is read-only by design.

### `PushStateCommand` + `PushStateHandler`

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

The handler never writes `_state.Current` directly. It computes the event, hands it to the stream, and lets the projection mutate. This is the F4 contract from framework-arch.

### `StatePushEvent` — 2-byte wire payload

```csharp
public sealed class StatePushEvent : WireEvent
{
    public byte NewKind  { get; }
    public byte PrevKind { get; }
}
```

Self-mirror per the event design rule. `Apply` uses `NewKind`; `Unapply` uses `PrevKind`. No polymorphism on the wire because the record itself carries zero data.

### Engine's `Evaluate` hook

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

## Push lifecycle

```
   handler receives PushStateCommand
        │
        ├─► prev.OnExit(scope)           → enqueue generated commands
        │
        ├─► stream.Append(StatePushEvent)
        │      └─ dispatches projection.Apply
        │           └─ state.Current = registry.Resolve(NewKind)
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

// framework reserves kinds 0–9
registry.Register<MatchNotStartedState>(0, () => new MatchNotStartedState());
registry.Register<PlayingState>        (1, () => new PlayingState());
registry.Register<MatchEndedState>     (2, () => new MatchEndedState());
```

A simplest game (e.g., Pile Counter) uses these three as-is: `NotStarted → Playing → Ended`. A phase-structured game (MTG / Hearthstone flavor) authors additional states starting at kind 10.

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

- **Zero-input deadlock.** A pure-resolution state (no `Actions`, OnEnter generates no commands when the world is empty) can deadlock — `IsCompleted` only fires after a command, but no command arrives. Pinned in `StatelessStateProofTests.CombatDamage_WithZeroAttackers_AutoCompletes`. Three viable fixes:
  - Re-evaluate after the push lands (vacuous completion advances flow).
  - Contract: pure-resolution states must emit ≥1 command in OnEnter.
  - Auto-emit a `NoOpCommand` when OnEnter returns empty.

  Settle before graduating to feature.

- **Match-end condition path.** `IStateFlow.NextAfter(MatchEndedState)` returns null; no game-over check sits in the flow itself. The framework needs an auto-emit pattern for game-over detection — likely a `MatchService` observer or an `EndMatchCommand` triggered when `Outcome != Undecided`. Out of scope for `IStateService`.

- **State-kind ID assignment.** Manual byte registration today. `[StateKind(N)]` attribute auto-discovered at bootstrap is the natural next step. Auto-allocation (assign based on registration order) is risky for cross-version replay; rejected.

- **State behavior placement.** Currently on the record itself (virtual methods). Alternative: per-kind handler classes (`IStateBehavior<TKind>`) registered against the kind. Cosmetic switch; no wire impact. Defer until ergonomics force it.

- **Block / causality threading on the wire.** When `PushStateCommand` runs, the `StatePushEvent` lands as a child block of the command's block. OnEnter-generated commands open *sibling* blocks at the next level. Confirm placement when the wire-block model from framework-arch lands in concrete form.

- **Hierarchy / pushdown / parallel regions.** Flat FSM for now. Tag-grouping handles "this state is combat-flavored" without hierarchy. Pushdown (push/pop interrupts for menus, modal decisions) deferred until a game forces it. Parallel regions stay as per-actor tag conventions (e.g., mulligan tags per Player) rather than parallel state machines.

- **Kind cardinality > 256.** `byte` covers 256 state kinds; phase-flavored games cap around 15. If a real game exceeds 256, switch to `ushort`. Not a structural change.

---

## Patch cascade

Lands together with framework-arch's cascade:

1. **New ADR (after the framework-arch ADR)** — locks F1 application to FSM: `IStateService`, `PushStateCommand`, `StatePushEvent`, baseline records.
2. **Refresh — `features/engine/models/07-match-state.md`** → `GameState` shape; drop slice wrapper; rename `MatchState` → `GameState`; update virtual signatures.
3. **Refresh — `features/engine/models/08-turn-flow.md`** — `ITurnFlow` → `IStateFlow`; returns `GameState?`.
4. **Refresh — `features/engine/models/03-game-rules-runtime.md`** — `Evaluate` returns `GameState?`; remove slice references.
5. **Refresh — ADR-0012 + `features/presentation/models/01-match-state-view-model.md`** — dispatch on `GameState` record type, not slice polymorphism.
6. **Sweep — `features/state/state.md`** — heavy rewrite or absorb into new feature folder.
7. **Glossary sweep** — retire `MatchState`, `MatchStateSlice`, `TransitionStateCommand`; add `GameState`, `IStateService`, `IStateFlow`, `IStateRegistry`, `StatePushEvent`, `PushStateCommand`.
8. **Sandbox refactor** — `MatchSingleton` on the spike's `World` becomes `MatchService` per framework-arch F1.

---

## Notes

- The earlier six-sketch walk (v1 polymorphic slice → v6 stateless records) lives in [`match-state-redesign.md`](match-state-redesign.md), now superseded. The historical evolution is preserved there for archeology — readers who want to see *why* v1–v5 were rejected can read that doc.
- The sandbox at `Assets/Spike/ECS/StateMachine/` is the current implementation. It still carries `MatchSingleton` on a spike-local `World` (pre-framework-arch shape) and will be refactored to `MatchService` when the patch cascade lands.
- No code edits made by this spike. Locked decisions only.
