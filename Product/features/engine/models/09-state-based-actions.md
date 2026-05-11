---
type: model
status: settled
references: [features/engine/models/03-game-rules-runtime.md, features/engine/models/07-match-state.md, features/engine/models/08-turn-flow.md, decisions/0009-engine-as-fsm.md]
tags: [#engine, #sba, #fsm]
---

# State-Based Actions — model

## What it is

The framework's pattern for **cross-cutting state-driven overrides** — checks that preempt the normal `ShouldExit` / `Flow` path because they apply regardless of which `MatchState` the match is in. **`ISBA`** is the contract; a registered list of SBAs runs at the start of the runtime's default `Evaluate` body. The first SBA to return a non-null `MatchState?` short-circuits — its return becomes the new state.

```csharp
public interface ISBA {
    MatchState? Check(IStateScope scope);   // null = no override
}
```

```
runtime.Evaluate(action, result):
  ┌──────────────────────────────────────────────┐
  │ foreach (sba in StateBasedActions)           │
  │   if (sba.Check(State) is { } over)          │
  │     return over;        ← first match wins   │
  └──────────────────────────────────────────────┘
                       │ (none matched)
                       ▼
              current.ShouldExit + Flow.NextAfter
                       (see engine/07 + engine/08)
```

Examples: game-over check (a player reached zero life), legend rule (duplicate legendary permanents), cleanup phase (lethal damage removes creatures), mulligan check (player still hasn't kept an opening hand).

## Contracts

### The interface

```csharp
public interface ISBA {
    MatchState? Check(IStateScope scope);
}
```

`null` = no override; the next SBA runs. Non-null = this is the new state; subsequent SBAs are not consulted on this drain step.

### Position in the runtime

The runtime base declares SBAs alongside Flow:

```csharp
public abstract class GameRulesRuntime<TDefinition> where TDefinition : GameRulesDefinition
{
    protected abstract ITurnFlow            Flow              { get; }
    protected abstract IReadOnlyList<ISBA>  StateBasedActions { get; }

    public virtual MatchState Evaluate(GameAction action, CommandResult result) {
        foreach (var sba in StateBasedActions)
            if (sba.Check(State) is { } over) return over;

        var current = State.Get<MatchStateSlice>().Current;
        return current.ShouldExit(action, result, State)
            ? Flow.NextAfter(current)
            : current;
    }
}
```

Concrete runtimes return their `IReadOnlyList<ISBA>` from the property. Typically populated via DI: each `ISBA` registered as `AsImplementedInterfaces`, the runtime injects `IEnumerable<ISBA>` and exposes it as a list.

### First non-null wins

SBAs run in **registration order**; the first to return non-null short-circuits. Order matters when multiple SBAs could fire simultaneously: register the most-preempting one first.

Reasoning: SBA priorities are stable within a game (a designer doesn't change "is the game over?" priority relative to "does the legend rule fire?" at runtime). Registration order is the simplest contract that gives game authors control. Richer priority composition deferred.

### What an SBA can and can't do

**Can:**

- Read state through the injected `IStateScope`.
- Return any `MatchState` subclass — game-defined or framework-defined (e.g., `GameOverState`).
- Compute over multiple slices, multiple entities, multiple players.

**Cannot:**

- Write state. `Check` is pure.
- Dispatch commands. The returned `MatchState` enters the system via `TransitionStateCommand`, which runs through the normal pipeline — this is the only side-effect channel.
- Hold mutable fields. SBAs are stateless (their inputs come from `IStateScope` every call).

### When to use an SBA vs. `ShouldExit`

An SBA fires *regardless* of the current state. A `ShouldExit` predicate is *local* to one state. The dividing line:

- **"This check applies in every state"** → SBA. Game-over, lethal damage, legend rule.
- **"This state exits when *this specific thing* happens"** → `ShouldExit`. Main phase exits on `EndTurnCommand`; combat declare-attackers exits on `CommitAttackersCommand`.

Some games will want both — e.g., `DeclareAttackersState.ShouldExit` returns true on `CommitAttackersCommand`, *and* a `GameOverSBA` is registered to catch lethal-damage-from-attackers triggers before the state's own exit path resolves.

### Boundaries — SBA vs. trigger vs. mutator

| Concept | When it fires | What it does |
|---|---|---|
| **Trigger** (`OnBefore<T>` / `OnAfter<T>`) | When a specific command type runs | Reactive; may modify, cancel, replace, or dispatch reactions |
| **Mutator** | When a payload is `Store.Execute`'d | Writes the new value of one slice |
| **SBA** | At the start of every `Evaluate` | Reads state; returns a new `MatchState` if a cross-cutting condition is met |
| **`ShouldExit`** | At every `Evaluate`, on the current state only | Returns true to advance Flow |

Triggers and mutators are **command-driven**. SBAs and `ShouldExit` are **state-driven** (they check state regardless of which command just ran). The framework picks command-driven vs. state-driven by what the rule *naturally is*; pushing a state-driven rule into a trigger fan-out leaks bugs (the rule misses any non-command path that produces the same state).

## Invariants

1. **SBAs are pure observers.** No state writes, no dispatch. Their only output is the returned `MatchState?`.
2. **First non-null wins; subsequent SBAs not consulted.** On a drain step where any SBA fires, later SBAs in the list don't run that step. They run again on the next `Evaluate` call.
3. **Registration order = run order.** DI registration order. Framework-default SBAs (if any ship) register first; games append after.
4. **SBAs preempt `ShouldExit` / `Flow`.** In the default `Evaluate` body. Games that need different ordering override `Evaluate`.
5. **SBA-returned state still flows through `TransitionStateCommand`.** The engine drain handles the type-equality check and dispatches the transition through the normal pipeline. SBA logic doesn't bypass history, triggers, or replay.

## Examples

### Pile Counter — win-condition SBA

```csharp
public sealed class PileCounterWinSBA : ISBA
{
    private readonly IActor _player;

    public PileCounterWinSBA(IReadOnlyList<IActor> actors) {
        _player = actors.Single();
    }

    public MatchState? Check(IStateScope scope) {
        var piles = scope.GetAll<PileState>();
        return piles.All(p => p.Counter == p.Target)
            ? new PileGameOverState(new MatchOutcome.Win(_player))
            : null;
    }
}
```

Registered in the game installer; the runtime's `StateBasedActions` returns `[winSBA]`. On every `Evaluate` step, the SBA polls all `PileState` slices; when every counter hits target, it returns `PileGameOverState`; the engine transitions and the match ends.

### Hearthstone-style lethal damage

```csharp
public sealed class LethalDamageSBA : ISBA
{
    public MatchState? Check(IStateScope scope) {
        var players = scope.GetAll<PlayerHealthState>().ToList();
        var alive   = players.Where(p => p.Hp > 0).ToList();

        if (alive.Count == players.Count) return null;           // nobody dead
        if (alive.Count == 0)             return new GameOverState(new MatchOutcome.Draw());
        if (alive.Count == 1)             return new GameOverState(new MatchOutcome.Win(alive[0].Actor));

        return null;   // partial deaths in multi-player; let other logic handle
    }
}
```

This is a true cross-cutting check — it doesn't matter whether the damage came from a `PlayCardCommand`, a `TriggerExecuteCommand`, an `EffectResolveCommand`, or any other source. The state-driven check catches them all.

### MTG-style legend rule

```csharp
public sealed class LegendRuleSBA : ISBA
{
    public MatchState? Check(IStateScope scope) {
        // Walk the battlefield, group by legendary name + controller, flag duplicates.
        var battlefield = scope.Get<ZoneContentsState>(WellKnownZones.Battlefield).Cards;
        var legendDuplicates = FindDuplicateLegends(scope, battlefield);

        if (!legendDuplicates.Any()) return null;

        // Open a choice state — the controlling player picks which to keep.
        return new LegendRuleChoiceState(legendDuplicates);
    }

    private IReadOnlyList<LegendDuplicate> FindDuplicateLegends(
        IStateScope scope,
        IReadOnlyList<CardRef> cards) { /* ... */ }
}
```

The SBA recognises the cross-cutting condition (regardless of how the duplicate got there — cast, copy effect, token-spawn, transform) and returns a choice state. `LegendRuleChoiceState`'s own `ShouldExit` predicate fires when the player commits a choice, after which Flow returns to wherever play resumed from.

### Composing multiple SBAs

```csharp
public sealed class StandardMagicRulesRuntime : GameRulesRuntime<StandardMagicRulesDefinition>
{
    protected override IReadOnlyList<ISBA> StateBasedActions { get; }

    public StandardMagicRulesRuntime(
        StandardMagicRulesDefinition definition,
        IStateScope                  state,
        ITurnFlow                    flow,
        LethalDamageSBA              lethal,
        LegendRuleSBA                legend,
        ZeroLibrarySBA               drawFromEmpty)
        : base(definition, state)
    {
        Flow              = flow;
        StateBasedActions = [lethal, legend, drawFromEmpty];   // registration order = priority
    }
}
```

The order encodes priority: game-over wins over legend-rule wins over draw-from-empty. Most-preempting first.

## Decisions

*Provenance: [ADR-0009](Product/decisions/0009-engine-as-fsm.md) — D5.*

## Open follow-ups

- **Priority composition.** Registration order is the current contract. Richer priority (named tiers, declared dependencies) deferred until a game forces composition complexity.
- **Per-state suppression.** "Don't fire `LegendRuleSBA` while in `CleanupState`." Today the SBA itself reads `MatchStateSlice.Current` and short-circuits if it's in a suppressed state. A declarative per-state suppression list on the runtime base could be cleaner; defer until multiple games want it.
- **MTG-strict SBA loop ordering.** MTG runs an SBA loop *between* trigger fold and reaction dispatch (per [`engine/03 Q3`](features/engine/models/03-game-rules-runtime.md)). Our SBAs run at the start of `Evaluate`, which is one step too late for strict MTG ordering. Workaround today: encode the SBA logic as game-state mutators reacting to damage / death triggers. Real fix: add a pre-after-fold evaluator hook. Deferred until a game forces it.
- **Framework-default SBAs.** Whether the framework should ship any SBAs (e.g., a generic `GameOverByOutcomeSBA` that fires when a runtime-supplied policy returns a non-null `MatchOutcome`) is open. Today there are none — all SBAs are game-authored.
- **SBA disable/enable at runtime.** Some games suspend specific SBAs during specific phases (e.g., "no legend-rule during cleanup"). Today the SBA short-circuits internally; a declarative on/off API deferred.
