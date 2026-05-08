---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/engine/models/03-snapshot-pipeline.md, features/presentation/models/01-presentation-loop.md, decisions/0007-five-validation-layers.md]
tags: [#actions, #snapshot, #templates, #model]
---

# Availability Snapshot — model

## What it is

The shape of `AvailableActionsSnapshot` — the typed `ActionTemplate` records the rule publishes through `IAvailableActions` and the presentation layer consumes to render UI affordances. Each command type has a paired `ActionTemplate` subtype carrying the per-action shape (targets, modes, parameters); a shared abstract base lets snapshot machinery store templates uniformly. This Model owns the snapshot's *shape*; the *production pipeline* (who writes when, validation posture) lives in [`engine/03-snapshot-pipeline.md`](features/engine/models/03-snapshot-pipeline.md).

The snapshot is the rule's **best-effort approximation** of what's currently legal. Per-binding `handler.Validate` runs at dispatch (authoritative); the snapshot may be stale. UI gates (`AffordanceMap`) project from the snapshot; dispatch catches stale bindings.

## Contracts

### The shape

```csharp
public sealed record AvailableActionsSnapshot(
    IReadOnlyList<ActionTemplate> Templates);

public abstract record ActionTemplate(
    Type      CommandType,    // the registered command type — replaces stringly-typed ActionId
    EntityId? Source);

// Examples — game-specific subtypes per command:

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

> **Boundary note.** `Ref<T>` and `Reference` fields on `ActionTemplate` subtypes are **boundary-correct** per [ADR-0005](Product/decisions/0005-objects-over-ids.md). `ActionTemplate` records are snapshot wire-form — transported to the presentation layer and consumed by `IIntentMapper` to build `GameCommand`s. Rule-side code constructing templates derives refs from held objects (`card.SelfRef`, `spell.SelfRef`).

Two simplifications:

- **`Type CommandType` instead of an `ActionId` parallel registry.** The handler registry is already keyed by command type; `IIntentMapper` dispatches on it; history serialises it. No parallel string-id registry.
- **Targets are `IReadOnlyList<Reference>` uniformly.** The entity model says everything targetable is addressable. Whether the entity is a card, slot, player, or zone is a per-action concern; the snapshot layer doesn't need to know.

### Building blocks

`TargetSlot`, `ModeOption`, `ParameterSpec` are reusable building blocks for `ActionTemplate` subtypes that need them. A `MoveCardTemplate` doesn't carry `Modes` or `Parameters` because it doesn't need them — typed shapes let each action surface only what it actually has.

```csharp
public sealed record TargetSlot(
    string                  Name,
    IReadOnlyList<Reference> Legal);

public sealed record ModeOption(
    string                    Label,
    IReadOnlyList<TargetSlot> TargetSlots);

public abstract record ParameterSpec(string Name);
public sealed record IntRange (string Name, int Min, int Max)                         : ParameterSpec(Name);
public sealed record OneOf<T> (string Name, IReadOnlyList<T> Choices)                 : ParameterSpec(Name);
public sealed record Subset<T>(string Name, IReadOnlyList<T> Pool, int Min, int Max)  : ParameterSpec(Name);
```

### `PlayerChoice`

The data the player committed to, bound by `IIntentMapper.Map(template, choice)` against a template:

```csharp
public sealed record PlayerChoice(
    int                                   TemplateIndex,
    int?                                  ModeIndex,
    IReadOnlyList<Reference>              Targets,
    IReadOnlyDictionary<string, object>   Parameters);
```

Shape is provisional under typed templates — may collapse to positional + parameters once `IIntentMapper` implementations stabilize.

### `IIntentMapper` becomes typed

The mapper switches on the typed template subtype to produce a well-formed command. Decision-unaware: produces the same command shape whether or not a decision is open.

```csharp
public sealed class PileCounterIntentMapper : IIntentMapper
{
    private readonly IStateScope state;
    public PileCounterIntentMapper(IStateScope state) => this.state = state;

    public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
    {
        MoveCardTemplate m => new MoveCardCommand(
            Card: m.Card,
            From: LocateCard(state.Get<PilesState>(), m.Card),
            To:   (Ref<Zone>)choice.Targets[0]),
        _ => throw new InvalidOperationException($"unknown template: {template}")
    };
}
```

No string-keyed slot lookup; the template's fields name themselves. `IStateScope` is injected for normal state-driven mapping (locating the card's current pile).

### Where eligibility lives

The snapshot is produced by the rule (`GameRulesRuntime.Evaluate` per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md)) and effects mid-execution (decision pickers). The rule decides:

1. **Per-template eligibility** — which template types are even relevant in the current game state. Cheap; filters whole categories.
2. **Per-binding eligibility** — for each template, enumerate legal bindings (targets, modes, parameter values). May or may not run `handler.Validate` against each binding; that's an opt-in cost the rule decides per-game.

The framework default is **on-demand validation** — `handler.Validate` is authoritative at dispatch and runs every time. The rule may *additionally* call `handler.Validate` during enumeration to prune templates whose every binding fails — but the framework doesn't auto-validate every candidate. See below-divider Q2 for the full options weighed.

### Where the shape strains

- **Cross-card combinatorics.** A template that needs to filter by combinations of cards across a hand forces enumeration of every binding; `Subset<T>` parameter supports it but per-binding validation cost is real.
- **Cost-side combinatorics.** Variable-cost spells (X-cost, choice of mana sources) multiply binding count. Template carries `IntRange` for X.
- **Distribute-among-many.** "Deal 5 damage divided among any number of targets" needs a per-target-amount map; templates that need this define their own bound shape (`WeightedSubset` not yet shipped — add when forced).
- **Cross-slot relational constraints.** "Select 2 from A and 1 from B with combined cost ≤ 5" — currently the rule encodes this in per-binding generation. No framework hook.
- **Per-player snapshots.** Currently the snapshot is global (or implicitly per-actor in single-actor games); per-player snapshots in multi-actor games defer until multiplayer lands.

## Invariants

Model-local invariants. (See `actions.md ## Invariants` for feature-level rules.)

- **One template type per command type.** Each `GameCommand` subclass has a paired `ActionTemplate` subtype.
- **`CommandType` discriminator is `Type`.** No parallel `ActionId` string registry.
- **`Source` is on the abstract base.** Every action originates from a source entity (or null for sourceless actions like `EndTurn`). Nullable by design.
- **Targets are `IReadOnlyList<Reference>` uniformly.** No per-game target-type baking.
- **Templates are immutable records.** Snapshot mutation requires producing a new template list and calling `Actions.Set`.
- **`IIntentMapper.Map` is decision-unaware.** It produces commands the same way whether or not a decision is open.

## Decisions

Linked: [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — Five validation layers, with `handler.Validate` authoritative. Per-template eligibility (the snapshot's first filter) is one of the five layers; `AffordanceMap` (presentation-side projection of this snapshot) is the second; `IntentMapper.Map` (well-formedness check during template→command mapping) is the third. Cross-cutting (engine + actions + presentation).

## Examples

### Pile Counter (the floor)

Single command type (`MoveCardCommand`); template per source pile.

```csharp
// Rule-side, inside Evaluate (per ADR-0006)
private AvailableActionsSnapshot BuildSnapshot()
{
    var lastMove  = State.Get<LastMoveState>().Move;
    var templates = new List<ActionTemplate>();

    foreach (var sourceRef in _pileRefs)
    {
        var contents = State.Get<ZoneContentsState>(sourceRef).Cards;
        if (contents.Count == 0) continue;

        var topCardRef  = contents[^1];
        var topInstance = State.Get<CardMapState>().InstanceOf(topCardRef);
        if (lastMove?.Card.Equals(topInstance) == true) continue;   // per-template suppression

        var destinations = _pileRefs
            .Where(p => !p.Equals(sourceRef))
            .Cast<Reference>()
            .ToList();

        templates.Add(new MoveCardTemplate(topInstance, destinations));
    }

    return new AvailableActionsSnapshot(templates);
}

Actions.Set(BuildSnapshot());
```

UI receives N templates (one per non-empty source pile, minus suppressed). Rendering: highlighted top cards, each with valid drop targets.

### Hearthstone-like (the ceiling)

State: active player's main phase. Hand has a vanilla minion (Fire Fly), a target spell (Fireball), a modal spell (Wrath: choose-one), an X-cost spell. Board: own minions A (ready) and B (sick); opponent minions C, D, hero H.

Six template types cover the turn surface; flat-`GameCommand` enumeration of the same state would be ~50 commands:

```csharp
new PlayMinionTemplate(
    Source: fireFlyRef,
    BoardSlots: legalBoardSlots),

new CastSpellTemplate(
    Spell:   fireballRef,
    Targets: [ minionA, minionB, minionC, minionD, heroH, ownHero ],
    XCost:   null,
    Modes:   []),

new CastSpellTemplate(
    Spell:   wrathRef,
    Targets: [],
    XCost:   null,
    Modes: [
        new ModeOption("deal 3",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
        new ModeOption("deal 1 + draw",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
    ]),

new CastSpellTemplate(
    Spell:   xBurnRef,
    Targets: [ minionA, minionB, minionC, minionD, heroH, ownHero ],
    XCost:   new IntRange("X", Min: 1, Max: 5),
    Modes:   []),

new MinionAttackTemplate(
    Source:  minionARef,
    Targets: [ minionC, minionD, heroH ]),

// summoning-sick minion B is excluded from the snapshot entirely
new EndTurnTemplate(),
```

Player picks "X-Burn for X=3 at Minion C." `IIntentMapper`:

```csharp
// PlayerChoice { TemplateIndex = 3, ModeIndex = null,
//                Targets = [ minionCRef ],
//                Parameters = { "X" → 3 } }

return new CastSpellCommand(
    Caster:     activePlayer,
    Spell:      xBurnTemplate.Spell,
    Targets:    choice.Targets,
    Parameters: choice.Parameters);
```

The resolved command carries fully-bound targets and parameters; the typed template carried the spec with its own field names. Six entries cover the whole turn surface.

## Open follow-ups

- **Modes vs. Parameters unification.** A mode is structurally a discrete-choice parameter that bundles target slots. Could fold into `Parameters` with a `ChoiceWithTargets` spec. Aesthetically cleaner; ergonomically heavier on `IIntentMapper`.
- **`ParameterSpec` taxonomy.** `IntRange`, `OneOf<T>`, `Subset<T>` cover the survey. Add `WeightedSubset` and `FreeText` when forced; structured-input cases (declare attackers/blockers as a multi-edge bipartite assignment) deserve their own spec.
- **Multi-stage / staged target picking.** Cross-slot constraints want either lazy slot resolution or `Validate`-as-final-arbiter. Pick one before MTG-shaped games arrive.
- **`Source` nullability.** Sourceless actions (`EndTurn`) make `Source` nullable. Could instead model `EndTurn` as having a player-source. Aesthetic; defer.
- **`PlayerChoice` shape under typed templates.** Targets sketched as `IReadOnlyList<Reference>` (positional). May want named for ergonomics in templates with several distinct slots. Pin when `IIntentMapper` is implemented.
- **Type erasure on `Parameters`.** `IReadOnlyDictionary<string, object>` is loose. Generic per-spec parameters would force compile-time matching but balloon types. Probably stay loose; `IIntentMapper` is the only consumer.
- **Per-player snapshots.** Multi-actor games may need per-actor template filtering or per-actor slices. Pin when multiplayer lands.

---

*Below: provenance — feature-internal decisions folded on 2026-05-08 from `features/actions/decisions/0001-typed-action-template.md` and `features/actions/decisions/0002-on-demand-validation.md` (both originally landed 2026-04-29). Single-feature scope (the snapshot's shape and validation posture are owned by actions; engine consumes the shape via `IAvailableActions`, presentation consumes via state subscription, but neither co-decides).*

## Options considered

### Q1 — Template typing (formerly ADR-0001 — folded)

The snapshot needs a way to describe "what command can be issued, against what targets, with what choices." Earlier sketches used a string-keyed `ActionId` plus a slot dictionary; this turned `IIntentMapper` into a reflection-flavored switch and made composition awkward.

- **A — String-keyed `ActionId` + slot dictionary.** Rejected — stringly-typed; hostile to evolution; reflection-flavored mapper.
- **B — Generic `ActionTemplate<TCommand>` only (no abstract base).** Rejected — templates must be storable in a uniform list.
- **C — Shared abstract base `ActionTemplate` (`CommandType: Type`, `Source: EntityId?`) with a typed subtype per command.** **Chosen.** Each command type has a paired `ActionTemplate` subtype; `IIntentMapper` switches on subtype.

### Q2 — Validation posture (formerly ADR-0002 — folded)

Eligibility was framed as "eager pre-validation vs lazy evaluation." Both extremes have problems: eager pays the cost up front for everything; lazy means presentation can offer commands the rule won't accept.

- **A — Eager pre-validation of every binding during snapshot build.** Rejected — cost grows with `targets × candidates × validators`.
- **B — Lazy evaluation only at dispatch.** Rejected — UI can't gate buttons accurately.
- **C — On-demand validation: per-template eligibility filters whole templates; per-binding `Validate` runs at dispatch (authoritative) and on-demand by the rule when it wants exhaustive coverage.** **Chosen.**

## Rationale

Q1's typed-base + per-command subtype shape gives compile-time `IIntentMapper` safety, eliminates the parallel `ActionId` registry, and makes template authoring symmetric to handler authoring (every new command ships a paired template). Q2's on-demand posture lets games choose their cost — floor games stay cheap; strict games opt into exhaustive enumeration. Together they keep the snapshot lean and the dispatch path authoritative.

## Consequences

- **Enables:** compile-time `IIntentMapper` safety; `ActionId` retirement (the `Type` is the identifier); snapshot builds at floor-game cost; opt-in exhaustive checks for games that demand them; rule-controlled validation cost.
- **Constrains:** every new command type ships a paired template; presentation must accept that "template offered" is weaker than "guaranteed valid"; `AffordanceMap` layer absorbs UI-side gating.
- **Cascade:** `IIntentMapper` consumes typed templates (per `actions/01`); `IAvailableActions` writes the snapshot (per `engine/03`); `AffordanceMap` reads the snapshot (per `presentation/01`).
