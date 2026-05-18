---
type: spike
status: superseded
forward-link: Product/spikes/state-service.md
tags: [#spike, #state, #fsm, #match-state, #design-pass, #superseded]
---

# Match State Redesign

> **Status: superseded (2026-05-18) by [`state-service.md`](state-service.md).** This spike walked six sketches (v1 polymorphic-slice → v6 stateless records) and in the process surfaced framework-foundational questions (where data lives, how mutation/wire/rollback relate) that were too large for state alone. Those framework-wide answers now live in [`framework-architecture.md`](framework-architecture.md); the state-specific conclusions live in [`state-service.md`](state-service.md). This doc is preserved for archeology — readers wanting to see *why* v1–v5 were rejected and how v6 emerged should read it; readers picking up implementation should read state-service instead. The original session ran 2026-05-17 on branch `claude/state-machine-design-hankF`.

## Reading guide

If you only have 10 minutes, read **TL;DR** + **Locked decisions** + **§v6 — Stateless states**. That's the conclusion and the shape you'd implement.

If you want the rationale: also read **Design evolution v1 → v6** — each sub-section names *why we moved on* from the prior sketch. That's where the disagreements with ADR-0009's settled shape get unpacked.

If you want the research evidence: read **Research synthesis**. It's compressed but cites the actual sources.

If you're picking up implementation: read **What v6 means for the framework** + **Patch cascade** + **Open items propagated forward**.

---

## TL;DR

**State is not an entity. State is a typed record with no data fields, owned by a per-role `IStateService`, pushed onto the event stream as a single 2-byte wire event (`StatePushEvent { byte NewKind; byte PrevKind }`).** Each game-authored state is a zero-field record subclass of `GameState` with virtual methods (`Actions`, `OnEnter`, `OnExit`, `IsCompleted`). All data that *would have* lived on a state — turn number, active player, selected attackers, outcome — lives on entities (per-card tags, a Match singleton, the Request service) instead.

Six sketches were walked. The settled shape (v6) replaces the slice-based `MatchState`/`MatchStateSlice` of ADR-0009 entirely. `TransitionStateCommand` becomes `PushStateCommand`; `ITurnFlow` stays in spirit (renamed `IStateFlow`); per-state ViewModels (ADR-0012) survive in shape but dispatch on record type instead of slice polymorphism. The wire-format polymorphism problem the user flagged dissolves — there's no polymorphism on the wire because state instances carry no payload.

Locked: F1 (foundation reshape — state not entity), Q3 (state and request are sibling services, not merged), Q4 (code-only authoring, DSL deferred), Q5 (determinism out of scope), Q6 (hooks are generators, skip on replay), Q7 (three baseline states ship: `MatchNotStartedState`, `PlayingState`, `MatchEndedState`). Foreclosed by F1: Q1 (mid-state mutation) and Q2 (cursor home).

One new framework concept is now load-bearing: a **Match singleton** that holds game-wide state (turn, active player, outcome). Its shape — real entity vs. service — is its own pass.

---

## Context

### Why this came up

ADR-0009 (engine-as-FSM) had landed and settled the framework's FSM shape: `MatchState` as an abstract `BaseState` record, wrapped in a `MatchStateSlice`, with transitions through `TransitionStateCommand` dispatched by the engine. That shape was built around the Scaffold `Store` and immutable slices. The vertical-slice spike (`spikes/vertical-slice-plan.md`) subsequently rebuilt the engine on an ECS-style World + entities + tags + per-role services, with 17 architectural locks (P1–P17) and then 6 more from the main-loop reshape (P18–P23). The new engine doesn't use Scaffold slices the way ADR-0009 assumed.

The mismatch surfaced when a fresh look at "what should states actually look like in the rebuilt engine?" started. Adjacent concerns piled on:

- "Different games will have different state shapes — Magic-complex to Hearthstone-simple. Can one baseline scale across?"
- "Designers should be able to extend states the way they extend cards (tags, variables)."
- "Client must react to state changes — the wire encoding has to be small, network-friendly, and non-polymorphic."
- "Rollback/replay must work the same for state as for entities."

The walk reopened the foundational shape of `MatchState`. After web research and multiple sketch iterations, the conclusion is that ADR-0009's shape doesn't survive the entity-based rebuild. This spike documents the alternative.

### Core tenets (must respect)

1. **Serializable event stream.** Every observable change is an event that goes on the wire / into history.
2. **Game state traversable back and forth.** Rollback, preview, what-if planning are first-class. Snapshot + replay must reconstruct state deterministically.
3. **(Almost) everything is an entity.** Definition + instance + tag bag + modifiers is the dominant primitive. State was unclear; this spike resolves it as *not* an entity.
4. **Different shapes, one baseline.** Hearthstone-simple to Magic-complex must scale from the same framework primitives.
5. **Framework for future consumers.** Designers shouldn't need to learn parallel rulesets for state vs. cards.
6. **Data-driven and code-authored authoring.** Both paths exist; even code-authored mechanics tend to dispatch commands / send data packets.

### Vocabulary distinction

Used throughout this spike — these are different concepts:

- **game-state** = the overall situation of the game (entities, services, pipeline, "what is happening"). Not the focus of this spike.
- **state** = a specific named FSM state ("Main Phase," "Combat," "Selecting Card," "Battle State"). What counts as a state is game-defined. **THIS is the focus.**

A "state" here is a position in the game's finite state machine. The game-state (all the entities, all the tags) is what *exists*; the state tells you *which phase of the FSM is currently active*.

---

## Research synthesis (compressed)

Three research tracks ran in parallel during the spike.

### Internal findings — non-spike docs

Before this spike, the framework already settled the following about state (ADR-0009 + ADR-0010 / 0011 / 0012 + engine Models 01 / 03 / 07 / 08):

- Engine *is* the FSM. `runtime.Evaluate(action, result) → MatchState`. Type-changes dispatch `TransitionStateCommand` through the pipeline.
- `MatchStateSlice` wraps the polymorphic `MatchState` because Scaffold's Store keys slices on exact type.
- `MatchState` is data + predicate: virtual `Actions`, `OnEnter`, `OnExit`, `IsCompleted`. No successor knowledge on the state.
- Successor knowledge in `ITurnFlow.NextAfter(current)`.
- Session lifecycle (NotStarted / InMatch / Ended) is a typed sealed `SessionState` hierarchy on `GameEngine.Session` — outside the Store, deliberately, since it predates the Store at bootstrap.
- `ISBA` retired 2026-05-11 (Magic-specific pattern; framework no longer ships it).
- Match-end auto-emit: framework ships `EndOfMatchState(MatchOutcome)`, engine auto-dispatches `EndMatchCommand` when transition lands there.

References: [`decisions/0009-engine-as-fsm.md`](../decisions/0009-engine-as-fsm.md), [`features/engine/models/07-match-state.md`](../features/engine/models/07-match-state.md), [`features/engine/models/08-turn-flow.md`](../features/engine/models/08-turn-flow.md), [`features/engine/models/03-game-rules-runtime.md`](../features/engine/models/03-game-rules-runtime.md), [`features/state/state.md`](../features/state/state.md).

The vertical-slice rebuild (P1–P23) doesn't yet have a state machine — it processes commands and effects but doesn't drive transitions. The shape from ADR-0009 was authored against the Scaffold-slice substrate; it doesn't translate directly to the rebuilt engine. That mismatch is what forced this redesign.

### Internal findings — prior spikes

- [`spikes/state-object-management.md`](state-object-management.md) (open) — ecosystem audit. Locked the framework on "ID-as-handle + external registry" pattern (ECS / Akka / Orleans precedent). Argued *no* production system puts behavior-bearing instances inside the immutable store; even MobX-State-Tree keeps two parallel views.
- [`spikes/entities-state-unification.md`](entities-state-unification.md) (accepted) — rebuilt the bridge between `Scaffold.Entities` and `Scaffold.States` around `Ref<T>` + a catalog. **Invariant: slices never hold live objects; they carry `Ref<T>`.** Catalog persists across snapshot/rollback.
- [`spikes/archive/fsm-architecture.md`](archive/fsm-architecture.md) (superseded by ADRs 0009–0012) — original FSM design pass. Locked D1–D5 + D12 (engine-is-FSM, `MatchStateSlice` wrapper, state-is-data + predicate, `ITurnFlow` for successors, ISBAs for cross-cutting, session as engine field).
- [`spikes/tag-identity-typed-hybrid.md`](tag-identity-typed-hybrid.md) — typed-hybrid tag identity. Tags as first-class runtime primitives. Pattern A (`static readonly TagKey<T>` values) for ordinary game tags; Pattern B (class-per-tag) for dual-role tags that double as event payloads.
- [`spikes/vertical-slice-plan.md`](vertical-slice-plan.md) (concluded) — 71 tests passing. P1–P23 architectural locks. Per-role services + scopes. World as pure substrate, no verbs. Pipeline (FIFO commands) + Stack (LIFO effects) as separate primitives. Engine API stays sync.

Three threads ran across these spikes:

- "ID-as-handle + external registry" is locked. Behavior lives in code keyed by type/tag, not on the instance.
- Slices never hold live objects. Wire data only.
- Tags are the designer-extension surface for entity-like things.

No prior spike asked "is the FSM state itself an entity?" The answer was *implicitly* "it's a slice" (per ADR-0009), but the slice-based substrate has since been retired.

### External findings — card-game engines

Web research surveyed Forge (MTG, Java), XMage (MTG, Java), Sabberstone + HearthSim (Hearthstone, C#), boardgame.io (TS), Slay the Spire (Java, modded). Compressed table:

| Engine | Where the FSM cursor lives | Hierarchy | Parallel regions | Designer extension |
|---|---|---|---|---|
| **Forge** | Enum + per-turn counters on `PhaseHandler` singleton | Flat (priority cycle ad-hoc) | None | Phase-range strings in card scripts |
| **XMage** | Two-level enums (Phase + Step) | Two-level | None | Subclass `Phase`/`Step` |
| **Hearthstone / Sabberstone** | **`GameTag.STATE` + `GameTag.STEP` + `GameTag.NEXT_STEP` on the Game entity** | Flat-ish | Yes (per-player `MULLIGAN_STATE`) | Tags on entities |
| **Slay the Spire** | `AbstractDungeon.CurrentScreen` enum + nested `GameActionManager` queue | Pushdown for menus, queue for combat | None | Hook subscriptions (BaseMod) |
| **boardgame.io** | String values on `ctx` (game → phase → turn → stage) | 4 levels | **Yes** — `setActivePlayers` | Open config + plugin middleware |

Universal across all five: **state data is small.** The "cursor" is an enum value or a string. Data that would have been "on the state" (selected attackers, turn number, mulligan progress) lives elsewhere — on per-entity tags, on a singleton game object, on a context object. None give the state itself a Ref + tag bag + modifier surface.

Sources: [Forge `PhaseType.java`](https://github.com/Card-Forge/forge/blob/master/forge-game/src/main/java/forge/game/phase/PhaseType.java), [XMage `Turn.java`](https://github.com/magefree/mage/blob/master/Mage/src/main/java/mage/game/turn/Turn.java), [Sabberstone `Game.cs`](https://github.com/HearthSim/SabberStone/blob/master/SabberStoneCore/src/Model/Game.cs), [HearthSim gamestate-protocol](https://github.com/HearthSim/hearthsim.info/blob/master/docs/gamestate-protocol.md), [boardgame.io phases](https://github.com/boardgameio/boardgame.io/blob/main/docs/documentation/phases.md), [BaseMod for Slay the Spire](https://github.com/daviscook477/BaseMod).

### External findings — turn-based / event-sourced patterns

- **Harel statecharts (1987)** → **SCXML (W3C, 2015)** adds three things to flat FSMs: hierarchy, orthogonal/parallel regions, history pseudo-states. Authoring-heavy but expressive.
- **XState (JavaScript)** is the most accessible modern implementation. State is JSON-serializable; actor model is central; explicit split between *snapshot persistence* (save latest) vs *event sourcing* (persist events as they happen, replay to reconstruct). On restore, "actions will not be re-executed because they are assumed to have been already executed. However, invocations will be restarted, and spawned actors will be restored recursively." That distinction is foundational to Q6.
- **Akka Persistent FSM → EventSourcedBehavior.** FSM transitions are persisted as domain events in an append-only journal, atomic with state changes. Recovery: load snapshot, replay events through `applyEvent`. Confirms the pattern that transitions belong as named events in the journal, not as state diffs.
- **Eulerfx + mdavey on FSM + event sourcing.** Factor the transition function `T: Input × State → Output × State` into `exec` (decide) and `apply` (perform). Event sourcing persists *Outputs*. `apply` must be pure and deterministic — any RNG/timer/wall-clock breaks replay.
- **Nystrom — Game Programming Patterns, State chapter.** Pushdown automata (stack of states for temporary states) and concurrent state machines (one FSM per orthogonal axis) are the canonical patterns when flat FSMs run out.

Sources: [Stately persistence](https://stately.ai/docs/persistence), [Akka persistence FSM](https://doc.akka.io/libraries/akka-core/2.7/typed/persistence-fsm.html), [Eulerfx gist](https://gist.github.com/eulerfx/4ac420a14422ac960222), [Nystrom — State](https://gameprogrammingpatterns.com/state.html), [W3C SCXML](https://www.w3.org/TR/scxml/).

### Pivotal datapoint — Hearthstone

The wire-format question pulled hardest on Hearthstone. **Hearthstone treats the FSM cursor as freely-mutable tag values on the Game entity.** Confirmed against actual source:

```python
# python-hearthstone/enums.py
GameTag.STATE      = 204
GameTag.STEP       = 19
GameTag.NEXT_STEP  = 198

State = IntEnum:    INVALID, LOADING, RUNNING, COMPLETE
Step  = IntEnum:    INVALID, BEGIN_FIRST, BEGIN_SHUFFLE, BEGIN_DRAW,
                    BEGIN_MULLIGAN, MAIN_BEGIN, MAIN_READY, MAIN_RESOURCE,
                    MAIN_DRAW, MAIN_START, MAIN_ACTION, MAIN_COMBAT, MAIN_END,
                    MAIN_NEXT, FINAL_WRAPUP, FINAL_GAMEOVER, ...  (21 values)
Mulligan = IntEnum: INVALID, INPUT, DEALING, WAITING, DONE
```

```csharp
// Sabberstone Game.cs — these are read/written on the Game entity via this[...]
public Step Step {
    get => (Step)_data[GameTag.STEP];
    set =>          this[GameTag.STEP] = (int)value;
}
public Step NextStep {
    set {
        this[GameTag.NEXT_STEP] = (int)value;
        GamesEventManager.NextStepEvent(this, value);
    }
}
public State State {
    get => (State)this[GameTag.STATE];
    set =>          this[GameTag.STATE] = (int)value;
}
```

The state-machine cursor is just an int-valued tag on the Game entity. Mutation is event-driven (the `NextStep` setter fires `NextStepEvent`). The Mulligan tag lives per-Player on each Player entity. **No separate "state machine" subsystem — the FSM is tags on entities, full stop.**

This validates "state cursor as freely-mutable values" and "data lives on entities" at production scale. It doesn't directly validate any of our intermediate sketches (which proposed *typed records per state* as a separate concept) — Hearthstone's "state" has no typed fields per kind. The v6 stateless-records shape ends up structurally closer to Hearthstone than to any of the intermediate sketches.

---

## Design evolution

Six sketches were walked. Each section names what changed and *why we moved on*.

### v1 — Polymorphic record on a Scaffold slice (ADR-0009 + tag surface)

Starting point: ADR-0009's settled shape, augmented with what the research suggested was missing — a **tag set** on the base record for grouping, an **interruption stack** for pushdown, and per-actor sub-state via existing entity tags for parallel regions.

```csharp
public sealed record MatchStateSlice(
    MatchState Current,
    ImmutableStack<MatchState> Stack
) : BaseState;

public abstract record MatchState : BaseState
{
    public virtual IReadOnlySet<Tag>          Tags          => ImmutableHashSet<Tag>.Empty;
    public virtual IReadOnlyList<Type>        Actions       => [];
    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) => false;
}

public sealed record DeclareAttackersState(IActor ActivePlayer) : MatchState
{
    public override IReadOnlySet<Tag> Tags => Tag.Set("combat", "decision-pending");
    public override IReadOnlyList<Type> Actions =>
        [typeof(DeclareAttackerCommand), typeof(EndDeclarationCommand)];
}

public sealed record TransitionStateCommand(MatchState NewState) : GameAction;
public sealed record PushStateCommand     (MatchState NewState)  : GameAction;
public sealed record PopStateCommand      ()                     : GameAction;
```

**Why we moved on:** v1 was anchored to the Scaffold Store / slice model, which the engine rebuild had retired. The vertical-slice work (P1–P23) shows the engine no longer uses slices the way ADR-0009 assumed — it uses an `EntityStore`, per-role services, and tag-driven queries. Continuing v1 would have meant either reviving the slice substrate or pretending the rebuild hadn't happened.

### v2 — State as an entity, with a StateService

Reframed to the entity world: state becomes an entity (like Card / Zone / Player) with state-specific tags + variables. A new `StateService` holds the cursor (`Ref<Entity> Current` + a stack).

```csharp
public class MainPhase        : ITagKey<bool> { }
public class CombatPhase      : ITagKey<bool> { }
public class DeclareAttackers : ITagKey<bool> { }
public class DecisionPending  : ITagKey<bool> { }

public static class StateVars
{
    public static readonly VariableKey<IActor>            ActivePlayer      = ...;
    public static readonly VariableKey<List<Ref<Entity>>> SelectedAttackers = ...;
}

public abstract class StateDefinition
{
    public abstract IReadOnlyList<ITagKey>    Tags             { get; }
    public virtual  IReadOnlyList<Variable>   InitialVariables => [];
    public virtual  IReadOnlyList<Type>       Actions          => [];

    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) => false;
}

public sealed class DeclareAttackersStateDef : StateDefinition
{
    public override IReadOnlyList<ITagKey> Tags =>
        [Tags.Of<CombatPhase>(), Tags.Of<DeclareAttackers>(), Tags.Of<DecisionPending>()];

    public override IReadOnlyList<Type> Actions =>
        [typeof(SelectAttackerCommand), typeof(EndDeclarationCommand)];
}

public interface IStateService
{
    Ref<Entity> Current { get; }
    IReadOnlyList<Ref<Entity>> Stack { get; }
    void Apply(TransitionStateCommand cmd);
    void Apply(PushStateCommand cmd);
    void Apply(PopStateCommand cmd);
}
```

**Why we moved on:** the user flagged a coupling concern. The `StateService` was carrying two roles in one type — **data owner** (holds `Current` + `Stack` refs) AND **controller** (applies transitions). To control entities, it had to know about specific state entities, find them, spawn/despawn them, swap pointers. That's the coupling — *two* sources of truth for "what state are we in?" (the service's `Current` field and whatever lived on the entities), and a service that knew about specific entities elsewhere in the world.

### v3 — Evaluate-driven core, runtime field, no stateful service

Stripped v2 to: state entities + `Evaluate` on the runtime + no service. Cursor lives on the runtime as a `Ref<Entity>` field. No stack (push/pop deferred). Pattern A tag values for game-specific state tags (not Pattern B class declarations).

```csharp
public abstract class StateDefinition
{
    public virtual IReadOnlyList<TagKey>      Tags             => [];
    public virtual IReadOnlyList<Variable>    InitialVariables => [];
    public virtual IReadOnlyList<Type>        Actions          => [];

    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) => false;
}

public interface IStateFlow
{
    StateDefinition? NextAfter(Ref<Entity> current, IStateScope scope);
}

public abstract class GameRulesRuntime
{
    public Ref<Entity> CurrentState { get; internal set; }
    public IStateFlow  Flow         { get; }

    public virtual StateDefinition? Evaluate(GameAction action, CommandResult result, IStateScope scope)
    {
        var def = DefOf(CurrentState);
        return def.IsCompleted(action, result, scope)
            ? Flow.NextAfter(CurrentState, scope)
            : null;
    }
}

public interface IStateScope
{
    Ref<Entity> Current { get; }
    bool Has(TagKey tag);
    T    Get<T>(VariableKey<T> key);
    IReadOnlyList<Type> Actions { get; }
}

public sealed record TransitionStateCommand(StateDefinition NewState) : GameAction;
```

Game-side example used Pattern A:

```csharp
public static class HearthstoneTags
{
    public static readonly TagKey<bool> MulliganPending = new(...);
    public static readonly TagKey<bool> MulliganDone    = new(...);
}

public sealed class MulliganStateDef : StateDefinition
{
    public override IReadOnlyList<TagKey> Tags => [/* state-kind tag */];
    public override bool IsCompleted(GameAction _, CommandResult __, IStateScope s)
        => s.World.Entities.With<Player>().All(p => p.Has(HearthstoneTags.MulliganDone));
}
```

**Why we moved on:** the user reframed the question. Who actually *needs* state to be an entity? The simulation needs the FSM rules; the client needs notification + valid actions + slim metadata. State-as-entity introduces all the entity overhead (Ref management, EntityStore membership, despawn lifecycle, snapshot integration) for something that's structurally just "a cursor + some metadata about where we are." The user proposed: state is a typed payload pushed onto the event stream as a custom block, owned by a service that holds the most-recently-pushed payload. State stops being an entity entirely.

### v4 — Push-based service, immutable record, not an entity

State is a typed record (not an entity). A per-role `IStateService` (sibling to `IRequestService`) owns the current state and emits pushes as custom block events on the wire. Records are immutable per push — to change metadata, push a new state.

```csharp
public abstract record GameState
{
    public virtual IReadOnlyList<Type>        Actions   => [];
    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction a, CommandResult r, IStateScope s) => false;
}

public interface IStateService
{
    GameState Current { get; }
    void Push(GameState next);
}

public interface IStateScope
{
    GameState Current { get; }
    IReadOnlyList<Type> Actions { get; }
}

public interface IStateFlow
{
    GameState? NextAfter(GameState current, IStateScope scope);
}

public abstract class GameRulesRuntime
{
    public IStateFlow Flow { get; }
    public virtual GameState? Evaluate(GameAction action, CommandResult result, IStateScope scope)
    {
        var current = stateService.Current;
        return current.IsCompleted(action, result, scope)
            ? Flow.NextAfter(current, scope)
            : null;
    }
}

public sealed record PushStateCommand(GameState NewState) : GameAction;

// Game-side records — typed, immutable per push
public sealed record MainPhaseState(int Turn, Ref<Player> Active) : GameState
{
    public override IReadOnlyList<Type> Actions => [typeof(PlayCardCommand), /* ... */];
}

public sealed record DeclareAttackersState(
    Ref<Player> Active,
    ImmutableList<Ref<Card>> Selected) : GameState
{
    // To pick another attacker: push a new DeclareAttackersState with extended list.
}
```

**Why we moved on:** the wire-format question pulled this apart. Records have typed fields (`int Turn`, `Ref<Player> Active`, etc.); the wire is unmanaged + network-friendly + can't carry polymorphic payloads directly. Encoding each typed record on the wire requires either per-state-kind wire messages (catalog grows per state kind) or a codec registry (kind byte + typed payload bytes). Both are workable but add real complexity — codec maintenance, schema versioning, double-declaration (engine record + wire serializer per kind). The user asked: is there a world where states are stateless, just IDs?

### v5 — Decisions integrated (mid-pass)

Same shape as v4, with the locked decisions from the design pass folded in: `PushStateCommand` goes through the command pipeline (uniform with everything else); the three baseline states are named; OnEnter/OnExit semantics on replay are clarified (they're generators — fired once at push time, their outputs land as events in history, replay walks the events rather than re-firing the hooks).

```csharp
public abstract record GameState
{
    public virtual IReadOnlyList<Type>        Actions   => [];
    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction a, CommandResult r, IStateScope s) => false;
}

public interface IStateService
{
    GameState Current { get; }
    void Push(GameState next);                  // sugar — constructs + dispatches PushStateCommand
}

public sealed record PushStateCommand(GameState NewState) : GameAction;
public sealed record StatePushBlock(/* polymorphic payload — encoding TBD */) : IWireBlock;

public sealed record MatchNotStartedState : GameState;
public sealed record PlayingState         : GameState;
public sealed record MatchEndedState(MatchOutcome Outcome) : GameState
{
    public override IReadOnlyList<Type> Actions => [];
}
```

Lifecycle pinned:

```
runtime.Evaluate(action, result, scope)
   → returns next: GameState
   → engine.Enqueue(new PushStateCommand(next))

PushStateHandler runs once in the pipeline:
   1. Current.OnExit(scope)    → enqueue returned commands
   2. stateService.Current = newState
   3. emit StatePushBlock on wire
   4. newState.OnEnter(scope)  → enqueue returned commands

Enqueued OnExit/OnEnter outputs flow through the normal pipeline, produce their own
wire events, get recorded in history. Replay walks events; hooks never re-fire.
```

**Why we moved on:** v5 still had typed fields on `GameState` records (Turn, ActivePlayer, Outcome, etc.). The wire-format polymorphism question remained — these typed fields had to be serialized somehow. The user asked the audit question: across every sample game, *does any state actually need typed fields, or can the data live elsewhere?* The audit answered: no state needs typed fields. All "would-be state data" has a better home on entities, the Match singleton, or the Request service.

### v6 — Stateless states (the locked shape)

`GameState` records carry **zero data fields**. They're identified by type alone. The wire emits a single 2-byte event (`StatePushEvent { byte NewKind; byte PrevKind }`). All data that would have lived on a state lives on entities (per-card tags), a Match singleton (turn / active player / outcome), or the Request service (pending decisions).

Full sketch below in §v6 — Stateless states.

---

## v6 — Stateless states

### Core types

```csharp
public abstract record GameState
{
    public virtual IReadOnlyList<Type>        Actions   => [];
    public virtual IReadOnlyList<GameAction>  OnEnter(IStateScope scope) => [];
    public virtual IReadOnlyList<GameAction>  OnExit (IStateScope scope) => [];
    public virtual bool IsCompleted(GameAction a, CommandResult r, IStateScope s) => false;
}

public interface IStateRegistry
{
    void      Register<T>(byte kind) where T : GameState, new();
    GameState Resolve(byte kind);
    byte      KindOf(GameState state);
}

public interface IStateService
{
    GameState Current { get; }
    void Push(GameState next);
}

public interface IStateScope
{
    GameState Current { get; }
    IReadOnlyList<Type> Actions { get; }
}

public interface IStateFlow
{
    GameState? NextAfter(GameState current, IStateScope scope);
}

public abstract class GameRulesRuntime
{
    public IStateFlow Flow { get; }
    public virtual GameState? Evaluate(GameAction action, CommandResult result, IStateScope scope) =>
        stateService.Current.IsCompleted(action, result, scope)
            ? Flow.NextAfter(stateService.Current, scope)
            : null;
}

public sealed record PushStateCommand(GameState NewState) : GameAction;
public sealed record PushStateResult() : CommandResult;
```

`GameState` records carry zero data fields. Behavior only. Same-instance equality is identity; same-kind comparison is type-check.

### The wire block

```csharp
[NetMessage]
public sealed class StatePushEvent
{
    public byte NewKind;
    public byte PrevKind;
}
```

One class. Two bytes. No polymorphism. Apply: `service.Current = registry.Resolve(NewKind)`. Unapply: `service.Current = registry.Resolve(PrevKind)`. Self-contained do/undo.

Everything else stays on the existing wire alphabet (TagSet / TagChange / Block / Pass / Effect / Request / etc.).

### Baseline states

```csharp
public sealed record MatchNotStartedState : GameState;
public sealed record PlayingState         : GameState;
public sealed record MatchEndedState      : GameState;

// Framework bootstrap registers:
registry.Register<MatchNotStartedState>(0);
registry.Register<PlayingState>(1);
registry.Register<MatchEndedState>(2);
```

Game-authored kinds start at byte 10 to leave room for framework growth.

A simplest game (Pile Counter) uses these three as-is. A Magic-flavor game replaces / extends with phase states.

### Simplified MTG worked example

Phase decomposition. Battle splits into substates because stateless states can't carry "selected attackers" — those live as tags on the creatures themselves.

```
Upkeep → Main1 → DeclareAttackers → DeclareBlockers → CombatDamage → Main2 → End → (next turn) Upkeep
```

State declarations — all empty records:

```csharp
public sealed record UpkeepState           : GameState { /* methods below */ }
public sealed record Main1State            : GameState { /* ... */ }
public sealed record DeclareAttackersState : GameState { /* ... */ }
public sealed record DeclareBlockersState  : GameState { /* ... */ }
public sealed record CombatDamageState     : GameState { /* ... */ }
public sealed record Main2State            : GameState { /* ... */ }
public sealed record EndState              : GameState { /* ... */ }
```

Three interesting bodies:

```csharp
public sealed record UpkeepState : GameState
{
    public override IReadOnlyList<Type> Actions => [typeof(PassCommand)];

    public override IReadOnlyList<GameAction> OnEnter(IStateScope s) =>
    [
        new UntapAllCommand(scope: ActivePlayer(s)),
        new DrawCardCommand(player: ActivePlayer(s)),
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
    public override IReadOnlyList<Type> Actions => [];   // pure resolution, no player input

    public override IReadOnlyList<GameAction> OnEnter(IStateScope s)
    {
        var attackers = s.World.Cards.With<IsAttacking>();
        var cmds = new List<GameAction>();
        foreach (var atk in attackers)
        {
            var blocker = ResolveBlocker(atk, s.World);
            var target  = blocker ?? DefendingPlayer(s);
            cmds.Add(new DealDamageCommand(atk, target, atk.Get(Power)));
        }
        return cmds;
    }

    public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
        s.World.Cards.With<IsAttacking>().All(c => c.Has<DamageResolved>());
}
```

Selected attackers live as the `IsAttacking` tag on each Creature card. Blockers as `Blocking: Ref<Card>` on each defender. Damage-resolved as `DamageResolved`. No state-record fields needed.

The `EndState` cleans the combat tags so the next turn starts clean:

```csharp
public sealed record EndState : GameState
{
    public override IReadOnlyList<Type> Actions => [typeof(PassCommand)];

    public override IReadOnlyList<GameAction> OnEnter(IStateScope s) =>
    [
        new ClearTagsCommand<IsAttacking>(),
        new ClearTagsCommand<Blocking>(),
        new ClearTagsCommand<DamageResolved>(),
        new IncrementTurnCommand(),    // writes Match.Turn += 1
        new SwapActivePlayerCommand(), // rewrites Match.ActivePlayer
    ];

    public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
        a is PassCommand;
}
```

Flow:

```csharp
public sealed class SimplifiedMtgFlow : IStateFlow
{
    public GameState? NextAfter(GameState current, IStateScope s)
    {
        if (GameOverCondition(s)) return new MatchEndedState();

        return current switch
        {
            MatchNotStartedState  => new UpkeepState(),
            UpkeepState           => new Main1State(),
            Main1State            => new DeclareAttackersState(),
            DeclareAttackersState => new DeclareBlockersState(),
            DeclareBlockersState  => new CombatDamageState(),
            CombatDamageState     => new Main2State(),
            Main2State            => new EndState(),
            EndState              => new UpkeepState(),
            _                     => null
        };
    }
}
```

Registration:

```csharp
registry.Register<UpkeepState>          (10);
registry.Register<Main1State>           (11);
registry.Register<DeclareAttackersState>(12);
registry.Register<DeclareBlockersState> (13);
registry.Register<CombatDamageState>    (14);
registry.Register<Main2State>           (15);
registry.Register<EndState>             (16);
```

### Sample wire stream — one combat transition

```
... player ends Main1 ...
[Block: Pass]
[/Block]
[StatePushEvent: prev=11(Main1), new=12(DeclareAttackers)]

... player marks two attackers, commits ...
[Block: ToggleAttacker source=Creature_A]
  [TagSet: Creature_A.IsAttacking = true]
[/Block]
[Block: ToggleAttacker source=Creature_B]
  [TagSet: Creature_B.IsAttacking = true]
[/Block]
[Block: CommitAttackers]
[/Block]
[StatePushEvent: prev=12(DeclareAttackers), new=13(DeclareBlockers)]

... defender assigns one blocker ...
[Block: AssignBlocker]
  [TagSet: Creature_X.Blocking = Creature_A]
[/Block]
[Block: CommitBlockers]
[/Block]
[StatePushEvent: prev=13(DeclareBlockers), new=14(CombatDamage)]

... damage resolves via OnEnter ...
[Block: DealDamage source=Creature_A target=Creature_X]
  [TagChange: Creature_X.Health 3 → 1]
  [TagSet: Creature_A.DamageResolved = true]
[/Block]
[Block: DealDamage source=Creature_B target=Player2]
  [TagChange: Player2.Life 20 → 17]
  [TagSet: Creature_B.DamageResolved = true]
[/Block]
[StatePushEvent: prev=14(CombatDamage), new=15(Main2)]
```

Every wire event is a known small concrete class. The state pushes are 2 bytes each. The entire combat sequence uses only the existing wire alphabet plus the new 2-byte `StatePushEvent`.

### Handler

```csharp
public sealed class PushStateHandler : ICommandHandler<PushStateCommand, PushStateResult>
{
    private readonly IStateService    state;
    private readonly IStateRegistry   registry;

    public PushStateResult Handle(PushStateCommand cmd, ICommandScope scope)
    {
        var old = state.Current;

        foreach (var c in old.OnExit(scope.AsStateScope()))
            scope.Enqueue(c);

        scope.Emit(new StatePushEvent {
            NewKind  = registry.KindOf(cmd.NewState),
            PrevKind = registry.KindOf(old)
        });
        state.Current = cmd.NewState;

        foreach (var c in cmd.NewState.OnEnter(scope.AsStateScope()))
            scope.Enqueue(c);

        return new PushStateResult();
    }
}
```

Five steps: exit hooks, emit wire event, swap, enter hooks, return.

Rollback for `StatePushEvent`: `state.Current = registry.Resolve(PrevKind)`. OnEnter/OnExit outputs were separate events; they get walked back individually via their own undo.

### Where displaced data lives

| Used in MTG sketch | Lives on | Notes |
|---|---|---|
| `ActivePlayer(scope)`, `DefendingPlayer(scope)` | Match singleton | tag or variable; mutated by `SwapActivePlayerCommand` |
| `Match.Turn` | Match singleton | mutated by `IncrementTurnCommand` |
| `Card.IsAttacking`, `Card.Blocking`, `Card.DamageResolved` | the Card entity | already-existing entity tag system |
| Outcome (winner / draw) | Match singleton | set just before pushing `MatchEndedState` |

The **Match singleton** is the one new framework concept this sketch makes load-bearing. Whether it's an entity in the store or a per-role service is its own design pass (see Open items).

---

## Locked decisions

From the design pass walk (`SESSION-CHECKPOINT.md` 2026-05-17 entry):

- **F1 — Foundation reshape (v3 → v4 → v6).** State is NOT an entity. State is an immutable typed record (per state-kind) — actually zero-data record after v6 — owned by a per-role `IStateService`, pushed into the event stream as a single 2-byte `StatePushEvent` block. Sibling pattern to `IRequestService`.
- **Q1 — Mid-state mutation:** *skipped — foreclosed by F1.* States are immutable per push; metadata changes happen by pushing a new state.
- **Q2 — Cursor home:** *skipped — foreclosed by F1.* `IStateService.Current` holds the most-recent pushed record; no entity-ref cursor.
- **Q3 = Sibling services, not merged.** State and Request follow the same architectural pattern (per-role service + custom block + immutable payloads) but stay distinct services. They don't collapse into a single subsystem.
- **Q4 = Code-only `GameState` records.** Game authors write record types by hand for now. Data-driven authoring (DSL, ScriptableObjects, etc.) deferred to the authoring feature when it materializes.
- **Q5 = Out of scope.** Determinism plumbing (RNG, clock) is baked into the world / services as needed and gets pinned in its own pass — not part of this state-machine model.
- **Q6 = Skip `OnEnter` / `OnExit` on replay.** Hook outputs land as events in history; events carry undo capability; replay walks the event stream rather than re-firing the hooks. Hooks are generators, not side-effects.
- **Q7 = Three baseline states shipped.** `MatchNotStartedState`, `PlayingState`, `MatchEndedState`. Games author additional states as needed.

---

## What v6 means for the framework

### Relation to ADR-0009 and the FSM-arch cascade

ADR-0009 (engine-as-FSM) was the foundation of the framework's FSM model. Its D1–D5 + D12 locks settled a slice-based shape: `MatchState` as a polymorphic `BaseState` record, wrapped in `MatchStateSlice`, with `TransitionStateCommand` as the framework command and `ITurnFlow.NextAfter` as the successor map.

v6 retires most of D1–D3:

- **D1 — Engine IS the FSM** *survives in spirit.* The runtime still owns `Evaluate(action, result)`; it now returns a `GameState?` instead of a `MatchState`. Engine still dispatches a transition command (renamed `PushStateCommand`).
- **D2 — `MatchStateSlice` wraps polymorphic `MatchState`** *retires.* No slice. No wrapper. The state lives on `IStateService` directly; the wire encoding is `StatePushEvent` (kind enum byte, not a slice).
- **D3 — State is data + predicate** *survives in spirit but inverted.* States have predicates (`IsCompleted`) but *no data fields* — data lives elsewhere.
- **D4 — Successor knowledge in `ITurnFlow.NextAfter`** *survives* (renamed `IStateFlow.NextAfter`).
- **D5 — ISBA** *already retired* per ADR-0009's 2026-05-11 refresh.
- **D12 — Session lifecycle as engine field** *survives unchanged.* `SessionState` typed hierarchy on `engine.Session` is independent of the match-state FSM.

The sub-ADRs cascade similarly:

- **ADR-0010 (validator / handler split)** — survives. `PushStateCommand` is just another `GameCommand`; validators and handler work the same way. The `MatchStateActionGate` validator reads `Current.Actions` off the record (was `MatchStateSlice.Current.Actions`).
- **ADR-0011 (effect orchestration + dispatch + requests)** — survives. `IRequestService` is the sibling pattern that informed `IStateService`; both follow the same shape.
- **ADR-0012 (presentation per-state ViewModels)** — survives in shape, repoints in mechanics. Per-state polymorphic `MatchStateViewModel<TState>` becomes per-state polymorphic `GameStateViewModel<TState>`; dispatch is on the record's runtime type (not slice polymorphism); `ViewModelRegistry` maps type → factory the same way.

### What changes for the engine

The vertical-slice rebuild (P1–P23) doesn't have an FSM today. v6 adds:

- `GameState` base record + `IStateService` + `IStateScope` + `IStateRegistry` + `IStateFlow` + `PushStateCommand` + `PushStateHandler` + `StatePushEvent`.
- Engine pump loop gains an `Evaluate` step after each command's resolution — runtime decides whether to push a new state.
- Match singleton concept (TBD shape — entity or service).

The pump loop in the current main-loop reshape (P18+) already calls `Evaluate` after every command resolution for SBA emission. The state-machine `Evaluate` slots into that same hook.

### Patch cascade

When v6 lands, these docs need updates (rough order):

1. **New ADR (likely 0014)** — supersedes ADR-0009's D1–D3. Establishes stateless-record push-based service model + sibling pattern with Requests. References this spike.
2. **New feature `features/states/`** (or absorbed into engine) — documents `IStateService`, `IStateScope`, `IStateRegistry`, `IStateFlow`, `GameState` base record, `PushStateCommand`/`PushStateResult`/`PushStateHandler`, `StatePushEvent`, the three baseline records, and the push lifecycle.
3. **Heavy rewrite — `features/engine/models/07-match-state.md`** → `GameState` record shape, drop slice wrapper. Rename `MatchState` → `GameState`. Update all virtual-member signatures.
4. **Refresh — `features/engine/models/03-game-rules-runtime.md`** — `Evaluate` returns `GameState?`; remove `MatchStateSlice` references; default body composes `IsCompleted` + `IStateFlow.NextAfter`.
5. **Refresh — `features/engine/models/01-game-engine.md`** — intake gate reads `Current.Actions` off record; transition is `PushStateCommand` not `TransitionStateCommand`; remove slice-based phrasing.
6. **Refresh — `features/engine/models/08-turn-flow.md`** — `ITurnFlow` → `IStateFlow`; returns `GameState?`.
7. **Refresh — ADR-0012 + `features/presentation/models/01-match-state-view-model.md` + `02-view-model-registry.md`** — dispatch on `GameState` record type, not slice polymorphism. Core idea survives; mechanics adjust.
8. **Audit — ADR-0010 / ADR-0011** — any references to `MatchState`, `MatchStateSlice`, `TransitionStateCommand` get repointed.
9. **Sweep — `features/state/state.md`** — heavy rewrite or absorb into the new feature. The Scaffold-Store-based shape doesn't survive.
10. **Sweep — `Product/ledgers/GLOSSARY.md`** — retire `MatchState`, `MatchStateSlice`, `TransitionStateCommand`; add `GameState`, `IStateService`, `IStateRegistry`, `IStateFlow`, `StatePushEvent`, `PushStateCommand`.
11. **Sweep — `Product/ledgers/OPEN-ITEMS.md`** — terminology + dangling references.
12. **Vertical slice integration** — current main-loop reshape (P18+) needs the `Evaluate` hook to call state-flow logic; need a state-service implementation; need the Match singleton.

---

## Open items propagated forward

- **Match singleton shape.** v6 makes this load-bearing. Holds game-wide singletons: turn number, active player, defending player, outcome. Two candidates:
  - **Entity in the world.** One `Match` entity per session, registered at bootstrap, holding tags/variables. Uniform with everything else.
  - **Per-role service.** `IMatchService` with typed accessors. Less uniform but maybe more ergonomic.

  Its own design pass. The lean is probably "entity" since it composes with the entity-store snapshot/rollback path for free.

- **State-kind ID assignment.** Sketch shows manual `registry.Register<T>(byte)` calls. Three alternatives:
  - Manual ints (current sketch). Simple, explicit, no magic.
  - `[StateKind(12)]` attribute on the record. Auto-discovered at bootstrap.
  - Auto-allocated. Bootstrap assigns IDs based on registration order. Risky for replay across versions.

  Manual is the safest starting point. Pin when authoring volume forces the question.

- **State behavior placement.** Currently on the record itself (virtual methods). Alternative: separate per-kind handler classes registered against the kind (`IStateBehavior<TKind>`) — more uniform with how effects are registered. Cosmetic switch; no wire impact. Defer until ergonomics forces it.

- **Wire format if state-kind cardinality grows beyond 256.** `byte` covers 256 kinds; if a game wanted more (unlikely — Magic-flavor caps around 15 phase states), switch to `ushort`. Not a structural change.

- **Hierarchy / pushdown / parallel regions.** All flagged as deferred during the design pass. Tag-grouping handles "this state is combat-flavored" without hierarchy. Pushdown is deferred until a real game forces it. Parallel regions stay as per-actor tag conventions for now.

- **OnEnter/OnExit re-fire on rollback.** v6 says replay walks events, not hooks. But for *forward* replay (re-running a session from the event log), is `OnEnter` ever called, or are its outputs read from the log? Spec: outputs are read from the log; `OnEnter` only fires on the *original* push. This needs explicit documentation when the cascade lands.

- **`StatePushBlock` wire-format edge cases.** What if push happens during a pump that's mid-block? Where does the `StatePushEvent` land in the block tree? Pushes are commands; commands open their own blocks; `StatePushEvent` is the inside of that block. Verify when the cascade lands.

---

## Notes

- This spike supersedes parts of [`spikes/archive/fsm-architecture.md`](archive/fsm-architecture.md) that hadn't already been superseded by ADRs 0009–0012. Specifically, the D1–D3 portions about `MatchState` shape.
- The Match-singleton question is the most important downstream open item. It's mentioned in 5+ places in v6 sketches; the cascade can't land cleanly until that shape is settled.
- The session that produced this spike walked the design pass per the `builder:design-pass` skill format. Locked decisions live in this spike's "Locked decisions" section and in `SESSION-CHECKPOINT.md`'s 2026-05-17 "Where we stopped" entry.
- All six sketches in §Design evolution are reproduced in full so future readers can see the genuine pivots — v1 was *not* a strawman to knock down; it was the synthesis of what ADR-0009 + tag/extension thinking would have looked like together. Each later sketch responds to a specific concern that the prior one couldn't address.
- The "battle splits into substates" insight from v6 is the key payoff of stateless states. In v4 (typed-field records), `DeclareAttackersState` carried `ImmutableList<Ref<Card>> Selected`. In v6, it's a zero-field record and `Selected` is just `world.Cards.With<IsAttacking>()`. This is the same shape Forge uses (creatures get tags; state cursor is just an enum). It validates that the entity model already had the answer.
