---
type: model
status: abandoned
archived_on: 2026-10-18
reason: "Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md."
references: [features/engine/archive/03-game-rules-runtime.md, features/engine/archive/05-game-factory.md, features/engine/archive/06-game-definition.md]
tags: [#engine, #gamerules, #definition]
---

> **Archived 2026-10-18.** Written for the old state model (slices, Ref<T>, MatchStateSlice) and never built. Read it for the open questions, not as the contract; the replacement design comes from Product/spikes/framework-roadmap.md.

# Game Rules Definition — model

## What it is

The pure-data half of the rules-layer split. **`GameRulesDefinition`** is a `[Serializable]`-attributed abstract data class (non-`UnityObject`) carrying authored, replay/save/network-safe configuration for a game-mode variant. Concrete subclasses add `[SerializeField]` config fields — starting life, hand size, format-specific knobs. One per game-mode variant (Standard, Commander, Pauper as three definition assets sharing one runtime class).

The Definition pairs with [`GameRulesRuntime<TDefinition>`](features/engine/archive/03-game-rules-runtime.md) via DI container open-generic resolution; the runtime carries the typed `Definition` and the framework's stateful brain. The Definition itself contributes no behavior.

```csharp
[Serializable]
public abstract class GameRulesDefinition
{
    // Concrete subclasses add [SerializeField] config fields.
}
```

## Contracts

### The two-type split

| Concern | `GameRulesDefinition` | [`GameRulesRuntime<TDefinition>`](features/engine/archive/03-game-rules-runtime.md) |
|---|---|---|
| Lifetime | Authored asset; reused across matches | Constructed per match; disposed with the engine |
| Mutability | Immutable for the runtime's lifetime | Stateful (configuration-only; zero gameplay state) |
| Serialization | Replay/save/network-safe | Not serialized — reconstructed from `Definition` + DI |
| Transport | Bundled inside `GameDefinition.Rules`; flows through `MatchSetup` | Resolved fresh per match via container open-generic |
| Authoring surface | `[SerializeField]` config knobs | Constructor injection + overridden contract methods |

### Pairing via container open-generic resolution

The factory pairs Definition to Runtime by reading the Definition's concrete type and resolving `GameRulesRuntime<>` for it:

```csharp
var definitionType = matchSetup.Game.Rules.GetType();        // MagicRulesDefinition
var runtimeType    = typeof(GameRulesRuntime<>).MakeGenericType(definitionType);
var runtime        = container.Resolve(runtimeType);
```

The Definition's concrete type is the discriminator. Game authors pick a runtime *by* picking a definition. Construction flow lives in [`engine/05-game-factory.md` §Behavior resolution](features/engine/archive/05-game-factory.md).

### Unity authoring

`GameRulesDefinition` is intentionally **not** a `ScriptableObject` — pure data preserves replay determinism across Unity domain reloads, simplifies network transport, and keeps the runtime's `Definition` field type-stable across editor recompiles. For inspector ergonomics, wrap in a thin asset (`GameRulesAsset : ScriptableObject` holding a serialized `GameRulesDefinition`) and have `GameDefinition.Rules` return the inner data class.

Per-asset versioning (migration of stale definitions, replay compatibility across schema changes) is the future authoring layer's concern (`bl-authoring-feature` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md)).

## Invariants

1. **Pure data, no behavior.** `[SerializeField]` config + read-only accessors. No methods that mutate state, reach the engine, or get invoked during play. Behavior lives on [`GameRulesRuntime<TDefinition>`](features/engine/archive/03-game-rules-runtime.md).
2. **`[Serializable]` and asset-friendly.** No Unity references, no event subscriptions, no held service handles.
3. **Immutable for the runtime's lifetime.** Constructor-injected once into the runtime; never reassigned.
4. **One Definition type ↔ one Runtime type.** Multiple Definition types pointing at one Runtime class is allowed (variant configurations); one Definition type pointing at multiple Runtime classes is not — DI resolution would be ambiguous.
5. **Carried inside `GameDefinition`, not on `MatchSetup`.** Per [`engine/06`](features/engine/archive/06-game-definition.md), `GameDefinition.Rules` is the canonical home.

## Decisions

*n/a — no elevated ADR.* (The two-type split is the outcome of Q1 in [`engine/03 §Options considered`](features/engine/archive/03-game-rules-runtime.md). This Model documents the data-side half of that split and carries no independent provenance.)

## Examples

```csharp
[Serializable]
public sealed class MagicRulesDefinition : GameRulesDefinition
{
    [SerializeField] private int    startingLife     = 20;
    [SerializeField] private int    startingHandSize = 7;
    [SerializeField] private string startingPhase    = "Untap";

    public int    StartingLife     => startingLife;
    public int    StartingHandSize => startingHandSize;
    public string StartingPhase    => startingPhase;

    public static MagicRulesDefinition Standard  => new() { startingLife = 20, startingHandSize = 7 };
    public static MagicRulesDefinition Commander => new() { startingLife = 40, startingHandSize = 7 };
}
```

Three game-mode variants — Standard, Commander, etc. — share one runtime class (`MagicRulesRuntime`). The DI installer registers the open generic once; per-match construction picks the right Definition value via `GameDefinition.Rules`.

## Open follow-ups

- **Definition versioning** — per-asset versioning (migration of stale definitions, replay compatibility across schema changes). Tracked under `bl-authoring-feature` in [`OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md).

---

*Below: provenance — n/a. The two-type split's options-weighing is documented in [`engine/03-game-rules-runtime.md` §Options considered](features/engine/archive/03-game-rules-runtime.md) (Q1). This Model is the data-side half of that decision and carries no independent provenance.*

## Options considered

*n/a — Q1 (rules-layer shape) lives in [`engine/03-game-rules-runtime.md`](features/engine/archive/03-game-rules-runtime.md).*

## Rationale

*n/a — see `## Contracts` above. The data-half of the rules layer is `[Serializable]` config; rationale for the two-type split lives in [`engine/03 §Q1 Rationale`](features/engine/archive/03-game-rules-runtime.md).*

## Consequences

*n/a — see `## Contracts` above. Combined consequences of the two-type split live in [`engine/03 §Consequences`](features/engine/archive/03-game-rules-runtime.md).*
