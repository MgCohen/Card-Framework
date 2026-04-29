# Action Availability and Mapping — The Snapshot Shape

This document defines the shape of the available-actions snapshot and the mapping from a player's choice back into a resolved `GameCommand`. It supersedes [doc 27 §5](27-presentation-loop.md) (snapshot element type) and reshapes parts of §4, §6, §9, §10, §11, §12 — the changes are summarised in [§7](#7-implications-across-existing-docs) below. Doc 27 is not edited by this doc; the follow-up edits live in their own pass.

The shape was sketched against Pile Counter (the floor) and a Hearthstone-like scenario (the ceiling for what this shape natively expresses), with prior-art reference to the [Kettle protocol](https://github.com/HearthSim/kettle-design/wiki/6.-Player-Simulator-interaction) used by [SabberStone](Old%20Docs/SabberStone-Architecture-Analysis.md%20%281%29) and [Fireplace](Old%20Docs/Fireplace-Architecture-Analysis.md%20%281%29).

Builds on:

- [25-gamerules-shape.md](25-gamerules-shape.md) — `GameRules` contract; `OnQueueDrained` returns the snapshot
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — sample game; degenerate projection of this shape
- [27-presentation-loop.md](27-presentation-loop.md) — presentation loop; introduced the `AvailableActions` slot

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

```csharp
public sealed record ActionAvailability(
    ActionId                     Id,
    EntityId?                    Source,
    IReadOnlyList<TargetSlot>    TargetSlots,
    IReadOnlyList<ModeOption>    Modes,
    IReadOnlyList<ParameterSpec> Parameters);

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

| Field | Meaning |
|---|---|
| `Id` | Which handler/action — what command type to construct |
| `Source` | Entity acting (the card cast, the minion attacking). Nullable for sourceless actions like `EndTurn` |
| `TargetSlots` | Per-slot legal entity sets. Empty when modes carry the targets |
| `Modes` | Discrete-choice variants, each with its own target slots. Empty for non-modal actions |
| `Parameters` | Free-form parameter slots (X-values, count picks, free choices) |

`ActionAvailability` is the **template**. `GameCommand` remains the resolved dispatch. The two are bridged by `PlayerChoice`:

```csharp
public sealed record PlayerChoice(
    int                                   ActionIndex,
    int?                                  ModeIndex,
    IReadOnlyDictionary<string, EntityId> Targets,
    IReadOnlyDictionary<string, object>   Parameters);
```

Direct analogue of Kettle's `(option_index, sub_option_index, target_id)` triple, with **named** slots instead of integer indices. Names survive serialisation meaningfully and let IntentMapper bind without positional assumptions.

The IntentMapper consumes `(snapshot[ActionIndex], PlayerChoice)` and produces a `GameCommand`. Symmetric to Kettle's server-side option formatter (`Util.KettleOptionToPlayerTask`) — it's the boundary that turns an indexed selection into an authoritative command.

---

## 3. Pile Counter Projection (the Floor)

Pile Counter has one action, one target slot, no modes, no parameters. `OnQueueDrained` becomes:

```csharp
public DrainResult OnQueueDrained(GameState state, IHandlerRegistry handlers)
{
    var snapshot = new List<ActionAvailability>();

    foreach (var pile in state.Piles.Where(p => p.Cards.Count > 0))
    {
        var top = pile.TopCard;
        if (state.LastMove?.Card == top.Id) continue;

        var destinations = state.Piles
            .Where(p => p.Id != pile.Id)
            .Select(p => (EntityId)p.Id)
            .ToList();

        snapshot.Add(new ActionAvailability(
            Id:          ActionIds.MoveCard,
            Source:      top.Id,
            TargetSlots: [ new TargetSlot("destination", destinations) ],
            Modes:       [],
            Parameters:  []));
    }

    return new DrainResult(snapshot, Dispatch: []);
}
```

Compared to [doc 26 §9.6](26-pile-counter-sample-game.md), three things change:

1. **Snapshot shrinks from cross-product to per-source.** Up to 12 candidate `MoveCardCommand`s collapse to ≤ 4 `ActionAvailability` entries.
2. **`AffordanceMap` becomes a one-liner** — the snapshot already is "card → destinations":

   ```csharp
   moveTargets = snapshot
       .Where(a => a.Id == ActionIds.MoveCard)
       .ToDictionary(
           a => (InstanceId)a.Source!,
           a => a.TargetSlots[0].Legal.Cast<PileId>().ToList());
   ```

3. **IntentMapper binds the named target slot** rather than just resolving an implicit `From`:

   ```csharp
   var avail = state.AvailableActions[choice.ActionIndex];
   return new MoveCardCommand(
       Card: (InstanceId)avail.Source!,
       From: state.LocateCard((InstanceId)avail.Source!),
       To:   (PileId)choice.Targets["destination"]);
   ```

The collapse is graceful: a zero-mode, single-slot, zero-parameter `ActionAvailability` is barely more verbose than a flat `GameCommand`.

The "twice in a row" rule moved from `handler.Validate` to `OnQueueDrained` because the rule needs to skip the *entire* `ActionAvailability` for that card, not reject individual `MoveCardCommand`s. `Validate` still owns it as the authoritative check; the rule is exercising the predicate during enumeration. This is the per-template / per-binding split — see [§6](#6-where-eligibility-lives).

---

## 4. Hearthstone-like Projection (the Ceiling for this Shape)

State: active player's main phase. Hand has Fire Fly (vanilla 2/2, cost 1), Fireball ("deal 6 to a target," cost 4), Wrath ("Choose One — deal 3 to a minion / deal 1 to a minion and draw," cost 2), and X-Burn (hypothetical: "pay X mana, deal X to a target"). Board: own minions A (ready) and B (sick); opponent minions C, D, hero H. 5 mana.

```csharp
new ActionAvailability(
    Id:          ActionIds.PlayMinion,
    Source:      fireFlyId,
    TargetSlots: [ new TargetSlot("slot", legalBoardSlots) ],
    Modes:       [], Parameters: []),

new ActionAvailability(
    Id:          ActionIds.PlaySpell,
    Source:      fireballId,
    TargetSlots: [ new TargetSlot("target",
        [ minionA, minionB, minionC, minionD, heroH, ownHero ]) ],
    Modes:       [], Parameters: []),

new ActionAvailability(
    Id:          ActionIds.PlaySpell,
    Source:      wrathId,
    TargetSlots: [],
    Modes: [
        new ModeOption("deal 3",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
        new ModeOption("deal 1 + draw",
            [ new TargetSlot("target", [ minionA, minionB, minionC, minionD ]) ]),
    ],
    Parameters: []),

new ActionAvailability(
    Id:          ActionIds.PlaySpell,
    Source:      xBurnId,
    TargetSlots: [ new TargetSlot("target",
        [ minionA, minionB, minionC, minionD, heroH, ownHero ]) ],
    Modes:       [],
    Parameters:  [ new IntRange("X", Min: 1, Max: 5) ]),

new ActionAvailability(
    Id:          ActionIds.MinionAttack,
    Source:      minionAId,
    TargetSlots: [ new TargetSlot("target", [ minionC, minionD, heroH ]) ],
    Modes:       [], Parameters: []),

// summoning-sick minion B is excluded from the snapshot entirely

new ActionAvailability(
    Id:          ActionIds.EndTurn,
    Source:      null,
    TargetSlots: [], Modes: [], Parameters: []),
```

Six entries cover the whole turn surface. Flat-`GameCommand` enumeration of the same state: Fireball would be 6 commands, Wrath 8 (2 × 4), X-Burn 30 (5 × 6), attacks 3 — about 50 versus 6.

Player picks "X-Burn for X=3 at Minion C." IntentMapper:

```csharp
// PlayerChoice { ActionIndex = 3, ModeIndex = null,
//                Targets = { "target" → minionCId },
//                Parameters = { "X" → 3 } }

return new CastSpellCommand(
    Caster:     activePlayer,
    Spell:      xBurnId,
    Targets:    new Dictionary<string, EntityId> { ["target"] = minionCId },
    Parameters: new Dictionary<string, object>   { ["X"]      = 3 });
```

The resolved command carries fully-bound targets and parameters; the snapshot carried the spec.

---

## 5. Where the Shape Strains

- **Cross-card combinatorics (MTG, "two Shocks on one creature").** Each Shock is its own `ActionAvailability`. The snapshot doesn't express *combined* plans. UI is fine — players act one at a time. AI is not — same limitation Forge accepts. Out of scope at this layer.
- **Cost-side combinatorics (MTG mana).** "Pay 1UUU" with hybrid sources, fetches, Mana Confluences. Expressible as a `Subset<EntityId>` over mana sources with min/max picks, but legality of the chosen subset is a mana-solver inside `Validate`. The snapshot declares the slot; the predicate inside `Validate` does the work.
- **"Distribute 5 damage among any number of creatures."** Needs a richer parameter shape — per-pick numeric weight, not a flat subset. Add a `WeightedSubset` spec when a game forces it.
- **"Target two different creatures."** Cross-slot relational constraints. Either compute slot B's `Legal` lazily (after slot A is chosen — implies multi-stage UI) or accept that snapshot lists are *loosely* legal and `Validate` catches relational violations. Probably the second.
- **Hidden-information / per-player snapshots.** Doesn't change this shape. Engine produces N snapshots, one per actor; `GameState.AvailableActions` becomes a per-player slot. Open elsewhere.

---

## 6. Where Eligibility Lives

Pile Counter's "twice in a row" rule sits in `OnQueueDrained` (suppresses the whole `ActionAvailability` for that card). Pile Counter's "source non-empty / source ≠ destination" sits in `handler.Validate`. Both are legality checks; they live in different layers. Why?

- **Validate operates on a fully-bound command** (`MoveCardCommand(Card, From, To)`). Once the source is fixed, `From != To` is a per-resolved-command check.
- **The "twice in a row" rule fixes the *whole template*** — there is no `(From, To)` binding for which the rule allows that card to move. Suppressing it at template level is honest; suppressing every per-binding command separately is wasteful.

So eligibility splits:

- **Per-template** — the rule decides which `ActionAvailability` entries appear at all. Cheap; one decision per source/action.
- **Per-binding** — `Validate` decides whether a chosen target/parameter combination is legal. Authoritative; same role as today.

This breaks doc 27 §9's line "the snapshot is built by *running* `handler.Validate` against every candidate." Strictly, the snapshot is built by:

1. The rule generates candidate `ActionAvailability` entries (per-template eligibility).
2. The rule asks `Validate` "is *some* binding of this template legal?" — not every binding, just at least one. (Optional; eager mode.)
3. The IntentMapper binds player choice into a resolved command.
4. `Validate` runs at dispatch on the resolved command. Authoritative.

Step 2 is the approximation. Step 4 is the truth. The snapshot can show actions that are loosely legal; `Validate` catches the bindings that aren't. The UI's `AffordanceMap` is still a projection of "things the rule and validate together think are likely legal."

This is a real architectural call. Two postures:

- **Eager** — rule pre-validates every binding while building the snapshot. Snapshot is exhaustive. Cost: O(templates × bindings). Pile Counter: cheap. Hearthstone: bounded. MTG: expensive.
- **Lazy** — rule emits templates with cheap eligibility; per-binding validation runs at dispatch only. Cost: O(templates). UI may show drag-drop affordances that fail at dispatch, surfaced as a toast.

Default proposal: **lazy with eager opt-in.** Games that benefit from exhaustive snapshots (debugger UIs, AI search trees, simple games) opt in; default keeps snapshots cheap.

---

## 7. Implications Across Existing Docs

These are follow-up edits, not done in this doc.

| Doc | Section | Change |
|---|---|---|
| 25 | §6 (`OnQueueDrained`) | `DrainResult.Snapshot: IReadOnlyList<ActionAvailability>` (was `IReadOnlyList<GameCommand>`) |
| 25 | §10 (Open Items) | Snapshot element-type question resolved to `ActionAvailability` |
| 26 | §9.1 (state shape) | `AvailableActions` type flips |
| 26 | §9.6 (the rule) | Rewrite snapshot construction per [§3](#3-pile-counter-projection-the-floor) |
| 26 | §9.3 (intent mapper) | Now binds named target slots |
| 26 | §10 | Add: snapshot stores templates; IntentMapper resolves |
| 27 | §4 State Shape | `AvailableActions: IReadOnlyList<ActionAvailability>` |
| 27 | §5 Element Type | Rewritten — templates, not resolved commands. Drop "snapshot mirrors history" symmetry argument |
| 27 | §6 AffordanceMap | Reads templates, not resolved commands |
| 27 | §9 Validation Layers | Add "per-template eligibility" tier; clarify snapshot construction is approximate, `Validate` at dispatch is authoritative |
| 27 | §10 End-to-End Trace | Step 6 (IntentMapper) becomes the *binding* step |
| 27 | §11 Decisions Locked | Flip the snapshot-element-type line |
| 27 | §12 `OnQueueDrained` Output | Snapshot type flips |

---

## 8. Open Items

- **Modes vs. Parameters unification.** A mode is structurally a discrete-choice parameter that bundles target slots. Could fold into `Parameters` with a `ChoiceWithTargets` spec. Aesthetically cleaner; ergonomically heavier on IntentMapper. Coin flip.
- **`ParameterSpec` taxonomy.** `IntRange`, `OneOf<T>`, `Subset<T>` cover the survey. Add `WeightedSubset` and `FreeText` when forced; structured-input cases (declare attackers/blockers as a multi-edge bipartite assignment) deserve their own spec.
- **Multi-stage / staged target picking.** Cross-slot constraints want either lazy slot resolution or `Validate`-as-final-arbiter. Pick one before MTG-shaped games arrive.
- **Eager vs. lazy binding validation.** [§6](#6-where-eligibility-lives). Default proposal: lazy with eager opt-in. Pin during implementation.
- **`Source` nullability.** Sourceless actions (`EndTurn`) make `Source` nullable. Could instead model `EndTurn` as having a player-source. Aesthetic; defer.
- **Type erasure on `Parameters`.** `IReadOnlyDictionary<string, object>` is loose. Generic per-spec parameters would force compile-time matching but balloon types. Probably stay loose; IntentMapper is the only consumer.

---

## 9. Related

- [25-gamerules-shape.md](25-gamerules-shape.md) — `GameRules` interface; `OnQueueDrained` shape
- [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) — Pile Counter; degenerate projection
- [27-presentation-loop.md](27-presentation-loop.md) — presentation loop; primary consumer
- [Kettle protocol — Player Simulator interaction](https://github.com/HearthSim/kettle-design/wiki/6.-Player-Simulator-interaction) — prior art
- `Old Docs/SabberStone-Architecture-Analysis.md` — Hearthstone task/option flow
- `Old Docs/Fireplace-Architecture-Analysis.md` — Hearthstone JSON option indexing
