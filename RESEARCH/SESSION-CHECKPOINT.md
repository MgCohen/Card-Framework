# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-28 (session 7)

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 28 written** — `ActionAvailability` shape for the available-actions snapshot. Snapshot stores **command templates**, not fully-resolved `GameCommand`s. Templates carry `Source`, `TargetSlots[]`, `Modes[]`, `Parameters[]`. Resolved against a `PlayerChoice` (named target/parameter dict) by the IntentMapper. Modelled on Hearthstone's Kettle `OptionsBlock` (prior art via SabberStone/Fireplace). Pile Counter projection is the floor (degenerate); Hearthstone projection is the ceiling for what this shape natively expresses. MTG-class cross-card combinatorics and cost-side mana solving sit outside the shape — `Validate` carries the predicate; snapshot declares the slot. Eager vs. lazy binding-validation framed but not picked (default proposal: lazy with eager opt-in).
- **Doc 27 written** — presentation/UI loop end-to-end. Resolved snapshot ownership: lives in `GameState.AvailableActions`. `AffordanceMap` is the game-specific UI projection. Validation has four layers. `OnQueueDrained` returns `DrainResult { Snapshot, Dispatch }`. **Now superseded in element-type by doc 28** (templates, not commands); doc 27 needs a follow-up edit pass per [doc 28 §7](28-action-availability-and-mapping.md).
- **Doc 26 written** — Pile Counter reference sample game. Used as the working reference for sketching framework pieces. §9 (`GameRules` sketch) needs minor edits to match doc 28's template shape — see doc 28 §7.
- **Doc 25 written + revised** — `GameRules` interface locked: four sync methods. `OnQueueDrained` returns `DrainResult`; `DrainResult.Snapshot` type now flips to `IReadOnlyList<ActionAvailability>` per doc 28 (follow-up edit).
- **Doc 24 written** — continuous and ongoing effects (attachment entity model).
- **Doc 23 written and amended** — replacement and prevention effects; `ITriggerPipeline` async; priority; sequential simultaneous events.
- **Full cross-document audit completed** — inconsistencies fixed across docs 09, 13, 15, 16, 17, 18, 19.

### Key decisions settled (cumulative)

- **`GameRules` is a sync, observer-shaped decision layer.** Returns data; never holds `GameEngine`.
- **`GameRules` holds zero game-affecting state.** Locked as contract; gameplay memory lives in `GameState`.
- **`OnIncomingCommand` is purely flow.** Pause/end gating + queue routing; never reads command body.
- **Validation moved from rule to handler.** `ICommandHandler<T,R>.Validate(cmd, state, out reason)` is authoritative.
- **Generic primitive + rule-enforced constraint.** Framework ships `MoveCardCommand` not `MoveTopCardCommand`.
- **Player input layer:** `PlayerIntent` (UI) → `IIntentMapper` (boundary) → `GameCommand` (engine).
- **Snapshot lives in state.** `GameState.AvailableActions`. Engine writes via internal mutator after each drain.
- **Snapshot stores templates, not resolved commands.** `IReadOnlyList<ActionAvailability>` (doc 28). `AffordanceMap` projects from templates; IntentMapper binds template + `PlayerChoice` → resolved command.
- **Four rejection paths:** AvailableActions (UI prophylactic) / `OnIncomingCommand` (engine flow) / `handler.Validate` (command legality, authoritative) / Before-fold `Cancel` (card-effect cancellation).
- **State pattern:** `GameState` is immutable, redux-like, likely Scaffold State. Mutators react to commands.
- **MTG-strict SBA ordering is a known semantic difference** (see doc 25 §5).

---

## The Active Open Question

**How is the snapshot produced?** The `ActionAvailability` *shape* is settled (doc 28 §2). Production is not. Three intertwined sub-questions:

1. **Where action enumeration lives.** Per-handler `Enumerate(state) → IEnumerable<ActionAvailability>`? Rule knows each action type explicitly? Separate action-descriptor registry? Composition of all three?
2. **Eager vs. lazy binding validation** (doc 28 §6). Per-template-only, or pre-validate every binding into the snapshot? Default proposal: lazy with eager opt-in. Pin during implementation.
3. **Pipeline composition.** Snapshot production sits next to handler-lookup → `Validate` → `Handle` → trigger folds. Probably merges with the existing "action pipeline cleanup" (#16) into one canonical pipeline doc.

This is the focus for the next session.

---

## Other Still-Open

Carried forward; some deferred while we work the active question.

- **Stateless vs stateful rule.** Does `OnIncomingCommand` receive `GameState` like the others, or hold it as a constructor-injected reference? Drives `IntakeContext` shape.
- **`GameState` contract / Engine ↔ Store ownership.** Does Engine own state with Store as reactive wrapper, or does Store (Scaffold State) own state and Engine reduce into it? Blocked on Scaffold State decision.
- **`EndGameCommand` semantics.** What does the engine do when it resolves? Bleeds into session lifecycle.
- **Game lifecycle design.** Session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle.** Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`IntakeContext.Pending` type** — `LinkedList<GameCommand>` sketched; confirm at engine implementation.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until forced.
- **Re-validation at dequeue** — validate at intake plus a second time before handler runs. Currently OK as double-validate.
- **`FilterTriggers` default impl** — identity for trigger-less games. Likely abstract base class with virtuals.
- **`CompositeGameRules` helper** — design when first composed-rules game needs it.
- **`AvailableActions` mutator** — likely framework-shipped since the snapshot type is canonical. Pin during implementation.
- **Hidden-info / per-player snapshots** — engine produces N snapshots per actor; `GameState.AvailableActions` becomes per-player slot. Surfaces with multiplayer.
- **Async decisions / multi-stage intents** — target pickers, mode choices, cost selection. Doc 28's shape carries the parameter spec; the staged-picking flow on top of it is undefined.

### Doc 28 internal opens (resolve when first composed-rules / MTG-class game arrives)

- **Modes vs. Parameters unification.** Could fold into a single `Parameters` list with `ChoiceWithTargets` spec.
- **`ParameterSpec` taxonomy.** `IntRange`, `OneOf<T>`, `Subset<T>` for now. Add `WeightedSubset`, `FreeText`, structured (declare-attackers/blockers) when forced.
- **Cross-slot relational constraints** ("target two different X"). Lazy slot resolution vs. `Validate`-as-final-arbiter. Lean toward second.
- **`Source` nullability.** Sourceless `EndTurn` makes `Source` nullable. Aesthetic; defer.
- **Type erasure on `Parameters`.** `IReadOnlyDictionary<string, object>` is loose. Probably stay loose; IntentMapper is the only consumer.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| **18** | **Snapshot production pipeline** | The active open question. Where enumeration lives (handler/rule/registry), eager vs. lazy binding validation, composition with handler-lookup → Validate → Handle. **Likely merges with #16.** |
| 16 | **Action pipeline cleanup** | Handler lookup, validate, execute, trigger folds — currently scattered across docs 18/19/22/25. Pull into one canonical doc; fold #18 in. |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice. Pull after #18/#16/#12 land. |
| 12 | **Game lifecycle design** | Session start, turn structure, phase transitions, end conditions — prerequisite for trigger subscription lifecycle. Best unblocked task after #18 lands. |
| 17 | **State-pattern decision** | Stateless vs. stateful rule re: how `OnIncomingCommand` receives `GameState`. Drives `IntakeContext` shape. |
| 19 | **Doc-alignment pass** | Apply [doc 28 §7](28-action-availability-and-mapping.md) edits to docs 25, 26, 27 (snapshot type flip, AffordanceMap reads templates, IntentMapper binding step). Mechanical. |
| 2 | **`GameState` contract** | Blocked on canonical state (Scaffold State). |
| 9 | **Revisit `StateChange` and state overall** | Blocked on canonical state. |
| 15 | **MTG-strict SBA ordering** | Decide whether to add a pre-after-fold evaluator hook or document the workaround. Defer until a game needs it. |

Done in earlier sessions: ~~1, 3, 4, 5, 6a/b/c, 8, 10, 11, 13, 14~~.

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [28-action-availability-and-mapping.md](28-action-availability-and-mapping.md) | `ActionAvailability` shape, Pile Counter + Hearthstone projections, eager vs. lazy validation, implications across docs 25/26/27 — **most recent** |
| [27-presentation-loop.md](27-presentation-loop.md) | UI loop end-to-end; superseded in element-type by doc 28 (snapshot is templates, not commands) |
| [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) | Pile Counter sample game spec — working reference; §9 sketch needs doc 28 alignment |
| [25-gamerules-shape.md](25-gamerules-shape.md) | `GameRules` interface; `DrainResult.Snapshot` type flips per doc 28 |
| [24-continuous-ongoing-effects.md](24-continuous-ongoing-effects.md) | Attachment entity model, turn signals, cleanup |
| [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) | `ITriggerPipeline`, `FilterTriggers`, replacement/prevention, priority, async |
| [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) | Layer hierarchy, two dispatch paths, async model |
| [21-trigger-reaction-system.md](21-trigger-reaction-system.md) | Trigger fold, subscription, OnBefore/OnAfter |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | End-to-end flow |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | CompositeAction, scope helpers |
| [06-open-questions.md](06-open-questions.md) | Current open question state |

Prior art (Old Docs):

- `Old Docs/SabberStone-Architecture-Analysis.md` — Hearthstone task/option flow, Kettle integration
- `Old Docs/Fireplace-Architecture-Analysis.md` — Hearthstone JSON option indexing

---

## How To Use This File

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
