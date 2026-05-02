---
type: model
status: settled
last-updated: 2026-05-01
builds-on: [features/actions/models/01-command-pipeline.md, features/engine/models/03-snapshot-pipeline.md]
tags: [#actions, #snapshot, #availability]
---

# Action Availability and Mapping — The Snapshot Shape

This document defines the shape of the available-actions snapshot and the mapping from a player's choice back into a resolved `GameCommand`. It supersedes [doc 27 §5](features/presentation/models/01-presentation-loop.md) (snapshot element type) and reshapes parts of §4, §6, §9, §10, §11, §12 — the changes are summarised in [§7](#7-implications-across-existing-docs) below.

[Doc 29](features/engine/models/03-snapshot-pipeline.md) further refines this doc: the generic `ActionAvailability` record is replaced by typed `ActionTemplate` subtypes per command type, and the eager-vs-lazy discussion in [§6](#6-where-eligibility-lives) is replaced by an "on-demand by the rule" posture. Refinements are noted inline.

The shape was sketched against Pile Counter (the floor) and a Hearthstone-like scenario (the ceiling for what this shape natively expresses), with prior-art reference to the [Kettle protocol](https://github.com/HearthSim/kettle-design/wiki/6.-Player-Simulator-interaction) used by [SabberStone](spikes/sabberstone-architecture.md) and [Fireplace](spikes/fireplace-architecture.md).

Builds on:

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` contract
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — sample game; degenerate projection of this shape
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — presentation loop; introduced the `AvailableActions` slot
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — typed templates, service-driven publication, on-demand validation

---

## 1. Why This Doc Exists

Doc 27 settled snapshot ownership (state slot, framework-defined) but locked the element type to fully-resolved `GameCommand`. The Pile Counter sketch makes that look fine — 12 candidates, one action type, discrete targets. It isn't fine for any game with more than one of:

- Multiple target slots per action (multi-target spells, attacker→target pairing)
- Modal cards (choose-one effects)
- Parameterised actions (X-cost spells, choose-N selections)
- Continuous parameters (pay any X, distribute N among any subset)

Enumerating "every legal `GameCommand`" in those cases is either combinatorially huge (Hearthstone), unbounded (X-cost), or just structurally wrong for the UI — the player picks card-then-target, not 30 separate "Lightning Bolt at A / B / C / …" entries.

Hearthstone's solution is the Kettle **OptionsBlock**: a hierarchy of `Option → main_option + sub_options → entity + targets[]`. A multi-target spell is *one* option with a `targets` array, not N options. Modal cards add `sub_options`. The client sends back `(option_index, sub_option_index, target_id)`. This doc adapts that pattern.

---

## 2. The Shape

> **Refined by [doc 29 §3](features/engine/models/03-snapshot-pipeline.md):** the single generic `ActionAvailability` record is replaced by **typed `ActionTemplate` subtypes per command type**, with a shared abstract base. `Id: ActionId` becomes `Type CommandType` (the command's type, used directly as the discriminator). The building blocks below (`TargetSlot`, `ModeOption`, `ParameterSpec`) remain valid as fields on subtypes that need them.

The base + a couple of subtype examples:

```csharp
public abstract record ActionTemplate(
    Type      CommandType,
    EntityId? Source);

public sealed record MoveCardTemplate(
    InstanceId              Card,
    IReadOnlyList<EntityId> Destinations)
    : ActionTemplate(typeof(MoveCardCommand), Card);

public sealed record CastSpellTemplate(
    InstanceId                Spell,
    IReadOnlyList<EntityId>   Targets,
    IntRange?                 XCost,
    IReadOnlyList<ModeOption> Modes)
    : ActionTemplate(typeof(CastSpellCommand), Spell);
```

The original generic shape — kept here for reference because the building blocks are still useful when an action does carry slots/modes/parameters:

```csharp
public sealed record TargetSlot(
    string                  Name,
    IReadOnlyList<EntityId> Legal);

public sealed record ModeOption(
    string                    Label,
    IReadOnlyList<TargetSlot> TargetSlots);

public abstract record ParameterSpec(string Name);
public sealed record IntRange (string Name, int Min, int Max)                         : ParameterSpec(Name);
public sealed record OneOf<T> (string Name, IReadOnlyList<T> Choices)                 : ParameterSpec(Name);
public sealed record Subset<T>(string Name, IReadOnlyList<T> Pool, int Min, int Max)  : ParameterSpec(Name);
```

| Field on base | Meaning |
|---|---|
| `CommandType` | Which command to construct — the registered command `Type`. Replaces the parallel `ActionId` registry |
| `Source` | Entity acting (the card cast, the minion attacking). Nullable for sourceless actions like `EndTurn` |

Per-subtype fields express the action's shape directly: `Destinations: IReadOnlyList<EntityId>` for a move, `Targets + Modes + XCost` for a spell, etc. Targets are uniformly `EntityId` lists ([doc 10's instance/entity model](features/entities/models/01-definition-instance-model.md)) — whether the entity is a card, slot, player, or zone is a per-action concern.

> **ADR-0005 boundary note.** `InstanceId` and `EntityId` fields in `ActionTemplate` subtypes are **boundary-correct**. `ActionTemplate` records are snapshot wire-form — transported to the presentation layer and consumed by `IntentMapper`. Rule-side code constructing templates derives IDs from held objects; the template stores IDs because it is the boundary form. See [ADR-0005](decisions/0005-objects-over-ids.md).

The template is the spec. `GameCommand` remains the resolved dispatch. The two are bridged by `PlayerChoice`:

```csharp
public sealed record PlayerChoice(
    int                                   TemplateIndex,
    int?                                  ModeIndex,
    IReadOnlyList<EntityId>               Targets,
    IReadOnlyDictionary<string, object>   Parameters);
```

The IntentMapper switches on the typed template subtype to bind:

```csharp
public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
{
    MoveCardTemplate  m => new MoveCardCommand(m.Card, state.LocateCard(m.Card), (PileId)choice.Targets[0]),
    CastSpellTemplate c => BindSpell(c, choice),
    _ => throw new InvalidOperationException($"unknown template: {template}")
};
```

No string-keyed slot lookup; each template subtype names its own fields. This is symmetric to Kettle's server-side option formatter (`Util.KettleOptionToPlayerTask`) but typed end-to-end.

---

## 3. Pile Counter Projection (the Floor)

Pile Counter has one action, one target slot, no modes, no parameters. With typed templates ([doc 29 §3](features/engine/models/03-snapshot-pipeline.md)) and service-driven publication ([doc 29 §2](features/engine/models/03-snapshot-pipeline.md)), `OnQueueDrained` becomes:

```csharp
public override DrainResult OnQueueDrained()
{
    var s = State.Current;
    var templates = new List<ActionTemplate>();

    foreach (var pile in s.Piles.Where(p => p.Cards.Count > 0))
    {
        var top = pile.TopCard;
        if (s.LastMove?.Card == top.Id) continue;

        var destinations = s.Piles
            .Where(p => p.Id != pile.Id)
            .Select(p => (EntityId)p.Id)
            .ToList();

        templates.Add(new MoveCardTemplate(top.Id, destinations));
    }

    Actions.Set(new AvailableActionsSnapshot(templates));
    return DrainResult.Empty;
}
```

Compared to a flat-command sketch, three things change:

1. **Snapshot shrinks from cross-product to per-source.** Up to 12 candidate `MoveCardCommand`s collapse to ≤ 4 `MoveCardTemplate` entries.
2. **`AffordanceMap` becomes a one-liner** — the template already is "card → destinations":

   ```csharp
   moveTargets = snapshot.Templates
       .OfType<MoveCardTemplate>()
       .ToDictionary(
           t => t.Card,
           t => t.Destinations.Cast<PileId>().ToList());
   ```

3. **IntentMapper switches on the template type:**

   ```csharp
   public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
   {
       MoveCardTemplate m => new MoveCardCommand(
           m.Card,
           state.LocateCard(m.Card),
           (PileId)choice.Targets[0]),
       _ => throw new InvalidOperationException($"unknown: {template}")
   };
   ```

The collapse is graceful: a single-field template (`Destinations`) is barely more verbose than a flat command.

The "twice in a row" rule moved from `handler.Validate` to `OnQueueDrained` because the rule needs to skip the *entire* template for that card, not reject individual `MoveCardCommand`s. `Validate` still owns it as the authoritative check; the rule is exercising the predicate during enumeration. This is the per-template / per-binding split — see [§6](#6-where-eligibility-lives).

---

## 4. Hearthstone-like Projection (the Ceiling for this Shape)

State: active player's main phase. Hand has Fire Fly (vanilla 2/2, cost 1), Fireball ("deal 6 to a target," cost 4), Wrath ("Choose One — deal 3 to a minion / deal 1 to a minion and draw," cost 2), and X-Burn (hypothetical: "pay X mana, deal X to a target"). Board: own minions A (ready) and B (sick); opponent minions C, D, hero H. 5 mana.

Each action gets its own typed template:

```csharp
new PlayMinionTemplate(
    Source: fireFlyId,
    BoardSlots: legalBoardSlots),

new CastSpellTemplate(
    Spell:   fireballId,
    Targets: [ minionA, minionB, minionC, minionD, heroH, ownHero ],
    XCost:   null,
    Modes:   []),

new CastSpellTemplate(
    Spell:   wrathId,
    Targets: [],
    XCost:   null,
    Modes: [
        new ModeOption("deal 3",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
        new ModeOption("deal 1 + draw",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
    ]),

new CastSpellTemplate(
    Spell:   xBurnId,
    Targets: [ minionA, minionB, minionC, minionD, heroH, ownHero ],
    XCost:   new IntRange("X", Min: 1, Max: 5),
    Modes:   []),

new MinionAttackTemplate(
    Source:  minionAId,
    Targets: [ minionC, minionD, heroH ]),

// summoning-sick minion B is excluded from the snapshot entirely

new EndTurnTemplate(),
```

Six entries cover the whole turn surface. Flat-`GameCommand` enumeration of the same state: Fireball would be 6 commands, Wrath 8 (2 × 4), X-Burn 30 (5 × 6), attacks 3 — about 50 versus 6.

Player picks "X-Burn for X=3 at Minion C." IntentMapper:

```csharp
// PlayerChoice { TemplateIndex = 3, ModeIndex = null,
//                Targets = [ minionCId ],
//                Parameters = { "X" → 3 } }

return new CastSpellCommand(
    Caster:     activePlayer,
    Spell:      xBurnTemplate.Spell,
    Targets:    choice.Targets,
    Parameters: choice.Parameters);
```

The resolved command carries fully-bound targets and parameters; the typed template carried the spec with its own field names.

---

## 5. Where the Shape Strains

- **Cross-card combinatorics (MTG, "two Shocks on one creature").** Each Shock is its own template. The snapshot doesn't express *combined* plans. UI is fine — players act one at a time. AI is not — same limitation Forge accepts. Out of scope at this layer.
- **Cost-side combinatorics (MTG mana).** "Pay 1UUU" with hybrid sources, fetches, Mana Confluences. Expressible as a `Subset<EntityId>` parameter over mana sources with min/max picks, but legality of the chosen subset is a mana-solver inside `Validate`. The template declares the slot; the predicate inside `Validate` does the work.
- **"Distribute 5 damage among any number of creatures."** Needs a richer parameter shape — per-pick numeric weight, not a flat subset. Add a `WeightedSubset` spec when a game forces it.
- **"Target two different creatures."** Cross-slot relational constraints. Either compute slot B's `Legal` lazily (after slot A is chosen — implies multi-stage UI) or accept that snapshot lists are *loosely* legal and `Validate` catches relational violations. Probably the second.
- **Hidden-information / per-player snapshots.** Doesn't change this shape. Engine produces N snapshots, one per actor; `GameState.AvailableActions` becomes a per-player slot. Open elsewhere.

---

## 6. Where Eligibility Lives

Pile Counter's "twice in a row" rule sits in `OnQueueDrained` (suppresses the whole template for that card). Pile Counter's "source non-empty / source ≠ destination" sits in `handler.Validate`. Both are legality checks; they live in different layers. Why?

- **Validate operates on a fully-bound command** (`MoveCardCommand(Card, From, To)`). Once the source is fixed, `From != To` is a per-resolved-command check.
- **The "twice in a row" rule fixes the *whole template*** — there is no `(From, To)` binding for which the rule allows that card to move. Suppressing it at template level is honest; suppressing every per-binding command separately is wasteful.

So eligibility splits:

- **Per-template** — the rule decides which template entries appear at all. Cheap; one decision per source/action.
- **Per-binding** — `Validate` decides whether a chosen target/parameter combination is legal. Authoritative; same role as today.

This breaks doc 27 §9's line "the snapshot is built by *running* `handler.Validate` against every candidate." Strictly, the snapshot is built by:

1. The rule generates candidate templates (per-template eligibility).
2. The rule may *optionally* call `Handlers.Validate` to prune templates whose every binding fails. This is on-demand, not automatic.
3. The IntentMapper binds player choice into a resolved command.
4. `Validate` runs at dispatch on the resolved command. Authoritative.

Step 2 is the approximation. Step 4 is the truth. The snapshot can show actions that are loosely legal; `Validate` catches the bindings that aren't. The UI's `AffordanceMap` is still a projection of "things the rule and validate together think are likely legal."

> **Refined by [doc 29 §2](features/engine/models/03-snapshot-pipeline.md):** the eager-vs-lazy framing below is replaced by **on-demand validation**. The rule does *not* automatically run `handler.Validate` against candidates during build. The default is per-template eligibility only; per-binding `Validate` is a tool the rule explicitly uses when it wants exhaustive coverage (e.g., a turn-start snapshot for a small game; a debugger UI). Scope of validation is a per-game decision, not a framework default.

The earlier eager-vs-lazy framing (kept here for reference; superseded by on-demand):

- **Eager** — rule pre-validates every binding while building the snapshot. Snapshot is exhaustive. Cost: O(templates × bindings). Pile Counter: cheap. Hearthstone: bounded. MTG: expensive.
- **Lazy** — rule emits templates with cheap eligibility; per-binding validation runs at dispatch only. Cost: O(templates). UI may show drag-drop affordances that fail at dispatch, surfaced as a toast.

---

## 7. Implications Across Existing Docs

Follow-up edits — applied in [doc 29's §8 implications table](features/engine/models/03-snapshot-pipeline.md) (with overlaps from this doc's original list). Both rounds of edits have been applied to docs 25, 26, 27.

| Doc | Section | Change |
|---|---|---|
| 25 | §2 Interface | Methods take only ephemeral data; services injected at construction. `IntakeContext` collapses |
| 25 | §6 (`OnQueueDrained`) | `DrainResult` shrinks to `Dispatch` only; snapshot writes through `IAvailableActions.Set` |
| 25 | §10 (Open Items) | State-pattern decision resolved (services injected) |
| 26 | §9.1 (state shape) | `AvailableActions` is `AvailableActionsSnapshot` |
| 26 | §9.6 (the rule) | Rewrite per [§3](#3-pile-counter-projection-the-floor) |
| 26 | §9.3 (intent mapper) | Switches on typed template subtype |
| 26 | §9.7 (engine intake) | `IntakeContext` collapsed |
| 26 | §9.8 (bootstrap) | Engine builder constructs services and injects into rule constructor |
| 26 | §10 / §11 | Templates instead of commands; service-driven publication; Q1/Q10/Q13 resolved |
| 27 | §4 State Shape | `AvailableActions` is `AvailableActionsSnapshot` |
| 27 | §5 Element Type | Rewritten — typed templates, not resolved commands |
| 27 | §6 AffordanceMap | Pattern-matches typed templates |
| 27 | §9 Validation Layers | Per-template eligibility tier added; snapshot is approximate; `Validate` at dispatch is authoritative |
| 27 | §10 End-to-End Trace | Step 12 is `Actions.Set`, not engine-writes-snapshot |
| 27 | §11 Decisions Locked | Flip element-type line; service-driven publication |
| 27 | §12 `OnQueueDrained` Output | `DrainResult` is `Dispatch` only |

---

## 8. Open Items

- **Modes vs. Parameters unification.** A mode is structurally a discrete-choice parameter that bundles target slots. Could fold into `Parameters` with a `ChoiceWithTargets` spec. Aesthetically cleaner; ergonomically heavier on IntentMapper. Coin flip.
- **`ParameterSpec` taxonomy.** `IntRange`, `OneOf<T>`, `Subset<T>` cover the survey. Add `WeightedSubset` and `FreeText` when forced; structured-input cases (declare attackers/blockers as a multi-edge bipartite assignment) deserve their own spec.
- **Multi-stage / staged target picking.** Cross-slot constraints want either lazy slot resolution or `Validate`-as-final-arbiter. Pick one before MTG-shaped games arrive.
- ~~**Eager vs. lazy binding validation.**~~ Resolved by [doc 29 §2](features/engine/models/03-snapshot-pipeline.md): on-demand by the rule, no automatic eager validation. Per-game decision.
- **`Source` nullability.** Sourceless actions (`EndTurn`) make `Source` nullable. Could instead model `EndTurn` as having a player-source. Aesthetic; defer.
- **`PlayerChoice` shape under typed templates.** Targets sketched as `IReadOnlyList<EntityId>` (positional). May want named for ergonomics in templates with several distinct slots. Pin when IntentMapper is implemented ([doc 29 §9](features/engine/models/03-snapshot-pipeline.md)).
- **Type erasure on `Parameters`.** `IReadOnlyDictionary<string, object>` is loose. Generic per-spec parameters would force compile-time matching but balloon types. Probably stay loose; IntentMapper is the only consumer.

---

## 9. Related

- [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) — `GameRules` interface
- [support/samples/pile-counter.md](support/samples/pile-counter.md) — Pile Counter; degenerate projection
- [features/presentation/models/01-presentation-loop.md](features/presentation/models/01-presentation-loop.md) — presentation loop; primary consumer
- [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) — typed templates, service-driven publication, on-demand validation
- [Kettle protocol — Player Simulator interaction](https://github.com/HearthSim/kettle-design/wiki/6.-Player-Simulator-interaction) — prior art
- `spikes/sabberstone-architecture.md` — Hearthstone task/option flow
- `spikes/fireplace-architecture.md` — Hearthstone JSON option indexing
