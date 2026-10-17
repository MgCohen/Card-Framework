---
type: model
status: superseded
superseded_by: 07-match-state.md
archived_on: 2026-05-10
reason: IAvailableActions service + AvailableActionsState slice retired per ADR-0009 (engine-as-FSM). MatchState.Actions: IReadOnlyList<Type> replaces the snapshot's role for the framework intake gate. Wire-form ActionTemplate and per-state UI projection reworked under ADR-0012 (presentation per-state ViewModels). Original content preserved below for historical record.
references: [features/engine/archive/03-game-rules-runtime.md, features/actions/archive/05-available-actions-snapshot.md, features/effects/archive/04-command-awaiter.md]
tags: [#engine, #snapshot, #available-actions, #archived]
---

> **Archived 2026-05-10.** Status: Superseded by [`Match State`](07-match-state.md) per [ADR-0009](../../../decisions/0009-engine-as-fsm.md). `IAvailableActions` service + `AvailableActionsState` slice retired. `MatchState.Actions: IReadOnlyList<Type>` replaces the snapshot's role for the framework intake gate; the wire-form `ActionTemplate` and presentation projection are reworked under ADR-0012 (presentation per-state ViewModels — *forthcoming*).
> Original content preserved below for historical record.

---

# Available Actions — model

## What it is

How the available-actions snapshot is **produced** and **published**. The shape of `AvailableActionsSnapshot` is settled in [`actions/05-available-actions-snapshot.md`](../../actions/archive/05-available-actions-snapshot.md); this Model settles its production: the runtime constructs typed `ActionTemplate`s and writes them through the `IAvailableActions` service. Validation stays at dispatch unless the rule explicitly opts a template into pre-validation. Snapshot production is its own pipeline triggered when the rule (or an effect) decides — not auto-fired by the engine on drain.

```csharp
public interface IAvailableActions
{
    void Set    (AvailableActionsSnapshot snapshot);    // wholesale replace
    void Append (IReadOnlyList<ActionTemplate> additional);   // additive; future composition
    void Clear  ();                                     // erase between phases / decisions
}
```

```
runtime.Evaluate (or effect mid-execution)
        │
        ▼
   Actions.Set(snapshot)
        │
        ▼   (internal mutator)
   AvailableActionsState slice
        │
        ▼
   IntentGateway / presentation read via IStateScope
```

## Contracts

### The service-driven model

Snapshot production is *not* a return value the engine collects from `OnQueueDrained` and writes to state. The rule (and effects) write the snapshot directly through a service the engine provides.

```csharp
public interface IAvailableActions
{
    void Set    (AvailableActionsSnapshot snapshot);
    void Append (IReadOnlyList<ActionTemplate> additional);
    void Clear  ();
}
```

- **`Set`** — replace the snapshot wholesale. The default writer.
- **`Append`** — additive contribution; useful for future composition where multiple rule modules each contribute templates.
- **`Clear`** — erase the snapshot (between phases, between decisions, etc.).

Behind the service the engine runs an internal mutator that writes to `AvailableActionsState`. The service is the only public write path.

### The `AvailableActionsState` slice

`IAvailableActions` writes to a framework-owned `BaseState` slice — the canonical home for the published snapshot. Reads go through `IStateScope.Get<AvailableActionsState>()` like any other slice.

```csharp
public sealed record AvailableActionsState(
    AvailableActionsSnapshot Snapshot) : BaseState;
```

- **Purpose.** Holds the current available-actions snapshot. Per ADR-0006, publication is purely runtime-driven — typically from inside `Evaluate` when the runtime detects its own follow-ups are exhausted, or from effects mid-execution.
- **Written by.** `IAvailableActions` service via a framework-internal mutator. Game authors write through the service, never directly.
- **Read by.** Gateway (pre-dispatch check), presentation layer, `GameRulesRuntime.Evaluate` for re-build.
- **Seeded.** `AvailableActionsState(AvailableActionsSnapshot.Empty)` at `Build()` (framework auto-seeds).

### Default trigger: `runtime.Evaluate` at drain-end

Per ADR-0006, `OnQueueDrained` is deleted. The runtime calls `Actions.Set(...)` from inside `Evaluate` when it detects "queue is about to empty" — its own state-based follow-ups are exhausted, no more work to enqueue. This is the canonical write moment for most games.

```csharp
public override IReadOnlyList<GameAction> Evaluate(GameAction action, CommandResult result)
{
    var followUps = ComputeStateBasedFollowUps();
    if (followUps.Any()) return followUps;

    // No follow-ups. Publish snapshot at this moment.
    Actions.Set(BuildSnapshot());
    return Array.Empty<GameAction>();
}
```

### Explicit triggers: anywhere the game wants

Effects mid-execution can override the snapshot through the same service (target pickers, choose-one prompts, mid-resolution priority grants). Phase-driven games re-publish on every phase change by reading state inside `Evaluate`. One-shot games (Stacklands) call `Actions.Set` once at bootstrap and never again.

The engine never auto-publishes. If nobody calls the service, `AvailableActions` does not change.

### Validation posture: on-demand

The rule does **not** automatically run `handler.Validate` against every candidate while building the snapshot. Per-binding validation runs at dispatch (authoritative) and any time the rule chooses to call it during enumeration to prune templates whose every binding fails. The cost is a deliberate game-author decision, not a framework default.

### Action templates — typed per command type

Typed templates per command type with a shared base. The base carries what the snapshot machinery and presentation projection need; subtypes carry per-action shape.

```csharp
public abstract record ActionTemplate(
    Type      CommandType,    // uses the registered command type directly
    EntityId? Source);

public sealed record MoveCardTemplate(
    Ref<Card>                Card,
    IReadOnlyList<Reference> Destinations)
    : ActionTemplate(typeof(MoveCardCommand), Card);

public sealed record CastSpellTemplate(
    Ref<Card>                 Spell,
    IReadOnlyList<Reference>  Targets,
    IntRange?                 XCost,
    IReadOnlyList<ModeOption> Modes)
    : ActionTemplate(typeof(CastSpellCommand), Spell);
```

Two simplifications:

- **`Type CommandType`.** The handler registry is already keyed by command type; `IntentMapper` dispatches on it; history serialises it. No parallel string-id registry.
- **Targets are `IReadOnlyList<Reference>` uniformly.** The entity model already says everything targetable is an entity. Whether the entity is a card, slot, player, or zone is a per-action concern; the snapshot layer doesn't need to know.

### Snapshot shape

The snapshot carries only the available templates:

```csharp
public sealed record AvailableActionsSnapshot(
    IReadOnlyList<ActionTemplate> Templates);
```

### Effect-side symmetry

Effects mid-execution write the snapshot through the same `IAvailableActions` service the rule uses, exposed on `IHandlerContext`.

## Invariants

1. **`IAvailableActions` is the only public write path.**
2. **Engine never auto-publishes.**
3. **No `Scope` discriminator on the snapshot.**
4. **Validation is on-demand.**
5. **Templates are typed per command type with a shared base.**
6. **`Reference`-typed fields on templates are boundary-correct (ADR-0005).**
7. **Effect-side symmetry.**

## Decisions

*n/a — folded as Q1–Q2 below.*

## Open follow-ups

(All open follow-ups resolved by archive: superseded by `MatchState.Actions` per ADR-0009.)

## Options considered

### Q1 — Where does the snapshot live?

The snapshot of currently-legal actions has to flow to presentation, network, and replay. Where does the snapshot *live*?

- **A — Side-channel observable owned by the rule.** Rejected — bypasses the state pattern.
- **B — Recompute on every read by presentation.** Rejected — cost is unbounded.
- **C — `AvailableActionsState` slice in `GameState`, written via the `IAvailableActions` service.** **Chosen.**

### Q2 — Snapshot writer surface

The snapshot has multiple potential writers — the runtime, effects mid-execution, the engine.

- **A — Engine auto-publishes after every command.** Rejected.
- **B — Runtime writes directly to `AvailableActionsState`.** Rejected.
- **C — Single `IAvailableActions` service exposed to both the runtime and effects.** **Chosen.**

## Rationale

(Historical — superseded by ADR-0009.)

## Consequences

(Historical — superseded by ADR-0009.)
