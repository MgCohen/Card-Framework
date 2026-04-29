# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-29 (session 8)

**Bridge decision landed (mid-session 8):** Scaffold shipped `com.scaffold.entities.states` (`feature/entities-state-bridge`). We commit to it. Per-entity variables are canonical Store slices (`EntityVariableState`); `StateEntity<TDef>` is a handle implementing `IReadOnlyEntity` / `IMutableEntity`. Closes the long-standing canonical-state question (doc 08 §"Canonical State", doc 10 §"Store And Entities Relationship", doc 04 §States, doc 06). Engine owns three integration responsibilities: handle re-hydration after `LoadSnapshot`, `Dispose` on entity death, and aware of diff-based structural-change subscriptions. None require further Scaffold changes.

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 29 written** — snapshot production pipeline. Resolves checkpoint task #18 (the active open question from session 7).
  - **Service-driven publication.** `IAvailableActions` (`Set`/`Append`/`Clear`) is the single write path for the snapshot. The rule writes from `OnQueueDrained` (default trigger); effects can override mid-execution; engine never auto-publishes. One-shot games (Stacklands) `Set` once at bootstrap. Phase-driven games (MTG) `Set` on phase enter / priority grant by reading state inside `OnQueueDrained`.
  - **Typed `ActionTemplate` per command type** with shared abstract base (`CommandType: Type`, `Source: EntityId?`). `ActionId` retired in favour of using the registered command `Type` directly. Targets are `IReadOnlyList<EntityId>` uniformly ([doc 10](10-entity-definition-instance-model.md) instance/entity model). IntentMapper switches on the typed subtype; no string-keyed slot lookup.
  - **On-demand validation, not eager / lazy.** Doc 28 §6's eager-vs-lazy framing is replaced. The rule does not auto-validate during build; per-template eligibility filters whole templates; per-binding `Validate` runs at dispatch (authoritative) and on-demand by the rule when it wants exhaustive coverage.
  - **Snapshot scope.** `AvailableActionsSnapshot { Scope, Templates }` with `Scope = FreeInput | DecisionPrompt(decisionId)`. Transitional — the eventual model is a richer FSM-shaped game state. Sufficient floor; revisit when async-decision-heavy games force the issue.
  - **`GameRules` shape change.** Abstract class with constructor-injected services (`IGameStateProvider`, `IAvailableActions`, `IIntakeQueue`, `IHandlerRegistry`). Methods take only ephemeral data. `IntakeContext` collapsed; `DrainResult` shrunk to `Dispatch` only. Resolves doc 26 Q1 (state-pattern decision).
- **Doc-alignment edits applied** to docs 25, 26, 27, 28. Each carries the new shape directly; doc 29 §8 is the canonical implications table.
- **Composition deferred.** Single rule per game for now; service surface supports it mechanically.
- **Doc 27 follow-up edits applied** (was an outstanding mechanical pass from session 7's task #19) — fold templates, validation tiers, end-to-end trace, decisions-locked, `OnQueueDrained` output.

### Key decisions settled (cumulative)

- **`GameRules` is sync, abstract class with services injected.** Methods take only ephemeral data. (doc 25 §2, doc 29 §5)
- **`GameRules` holds zero game-affecting state.** Services are configuration; gameplay memory still in `GameState`. (doc 25)
- **Snapshot lives in state.** `GameState.AvailableActions: AvailableActionsSnapshot` (Scope + typed templates). (docs 27, 29)
- **Snapshot writer is `IAvailableActions`.** Rule and effects share the same service. Engine never auto-publishes. (doc 29 §2)
- **Snapshot element type is typed `ActionTemplate` per command.** Each command has a paired template; CommandType discriminator. (docs 28 §2, 29 §3)
- **On-demand validation.** No automatic eager pre-validation; rule decides. (doc 29 §2)
- **`OnIncomingCommand` is purely flow.** Pause/end gating + queue routing; never reads command body. (doc 25 §3)
- **Validation moved from rule to handler.** `ICommandHandler<T,R>.Validate` is authoritative at dispatch.
- **Generic primitive + rule-enforced constraint.** Framework ships `MoveCardCommand` not `MoveTopCardCommand`.
- **Player input layer:** `PlayerIntent` (UI) → `IIntentMapper.Map(template, choice)` → `GameCommand`. Typed switch on template subtype. (doc 28 §2)
- **Five validation layers:** per-template eligibility (rule) / AffordanceMap (UI) / IntentMapper (well-formedness) / `handler.Validate` (authoritative) / `OnIncomingCommand` (flow). (doc 27 §9)
- **State pattern:** `GameState` immutable, redux-like, on Scaffold States. Mutators react to commands. `IAvailableActions` writes via internal mutator.
- **Entities ↔ States relationship:** Store canonical, Entities-as-API via `com.scaffold.entities.states` bridge. Per-entity variables are `EntityVariableState` slices; `StateEntity<TDef>` is a handle implementing `IReadOnlyEntity` / `IMutableEntity`. (doc 08, doc 10)
- **MTG-strict SBA ordering is a known semantic difference.** (doc 25 §5)

---

## The Active Open Question

**Effect-flow doc.** Effects mid-execution need `IAvailableActions` (target pickers publish decision-scoped snapshots) and `IGameStateProvider`. Likely through `IHandlerContext`. The full effect lifecycle — async orchestration, decision-id threading, await semantics, suspension/resumption — is currently scattered across docs 22 and the implicit handler-context placeholders in doc 26. Needs a single canonical effect-flow doc.

This is also a prerequisite for properly modelling the snapshot scope as a state machine (the transitional `FreeInput`/`DecisionPrompt` will be replaced by typed states once we know how decisions actually flow).

This is the focus for the next session. Tracked as task #20.

---

## Other Still-Open

Carried forward; some deferred while we work the active question.

- **`IGameStateProvider` shape** — `State.Current` returns the immutable state; subscriptions probably live on the Store. Pin at engine implementation.
- **`IIntakeQueue` shape** — same ops as old `LinkedList<GameCommand>` Pending. `CurrentlyExecuting` lives here or on state provider. Pin at engine implementation.
- **`GameState` contract / Engine ↔ Store ownership.** Store (Scaffold States) owns state; Engine dispatches commands that translate to mutator payloads. Remaining work is the concrete slice catalog (turn, phase, decisions, zones, available actions) and the `InstanceId → (definition, store)` registry the engine maintains for handle re-hydration. No longer blocked.
- **`EndGameCommand` semantics.** What does the engine do when it resolves? Bleeds into session lifecycle.
- **Game lifecycle design.** Session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle.** Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until forced.
- **Re-validation at dequeue** — validate at intake plus a second time before handler runs. Currently OK as double-validate.
- **`FilterTriggers` default impl** — virtual identity default, now possible since `GameRules` is an abstract class.
- **`CompositeGameRules` helper** — design when first composed-rules game needs it.
- **Hidden-info / per-player snapshots** — engine produces N snapshots per actor; `GameState.AvailableActions` becomes per-player slot. Surfaces with multiplayer.
- **Snapshot scope as state machine** — transitional `FreeInput`/`DecisionPrompt` will be replaced; revisit with effect-flow doc.

### Doc 28 internal opens (resolve when first composed-rules / MTG-class game arrives)

- **Modes vs. Parameters unification.** Could fold into a single `Parameters` list with `ChoiceWithTargets` spec.
- **`ParameterSpec` taxonomy.** `IntRange`, `OneOf<T>`, `Subset<T>` for now. Add `WeightedSubset`, `FreeText`, structured (declare-attackers/blockers) when forced.
- **Cross-slot relational constraints** ("target two different X"). Lazy slot resolution vs. `Validate`-as-final-arbiter. Lean toward second.
- **`Source` nullability.** Sourceless `EndTurn` makes `Source` nullable. Aesthetic; defer.
- **`PlayerChoice` shape under typed templates.** Targets sketched as positional `IReadOnlyList<EntityId>`. May want named for ergonomics. Pin when IntentMapper is implemented.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| **20** | **Effect-flow canonical doc** | The active open question. Full lifecycle: `IHandlerContext` shape, `IAvailableActions` access from effects, decision-id threading, await semantics, suspension/resumption. Pulls from docs 22 (async layer), 26 (handler context placeholders), 29 §7 (effect-side symmetry). Prerequisite for the state-machine model that replaces snapshot scope. |
| 16 | **Action pipeline cleanup** | Handler lookup, validate, execute, trigger folds — currently scattered across docs 18/19/22/25. Pull into one canonical doc. May merge with #20 since both are about the per-command pipeline + effect orchestration. |
| 12 | **Game lifecycle design** | Session start, turn structure, phase transitions, end conditions — prerequisite for trigger subscription lifecycle. Best unblocked task after #20 lands. |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice. Pull after #20/#16/#12 land. |
| 2 | **`GameState` contract** | Concrete slice catalog (turn, phase, decisions, zones, available actions) on top of Store. Entity slices use `EntityVariableState` via the bridge. |
| 9 | **Revisit `StateChange` and state overall** | Unblocked by bridge decision. Folds into task #2. |
| 15 | **MTG-strict SBA ordering** | Decide whether to add a pre-after-fold evaluator hook or document the workaround. Defer until a game needs it. |

Done in earlier sessions: ~~1, 3, 4, 5, 6a/b/c, 8, 10, 11, 13, 14, 18, 19~~.

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [29-snapshot-production-pipeline.md](29-snapshot-production-pipeline.md) | **Most recent.** Service-driven publication, typed templates, on-demand validation, services-injected `GameRules`. Resolves task #18. |
| [28-action-availability-and-mapping.md](28-action-availability-and-mapping.md) | Snapshot shape (typed templates with shared base). Refined inline by doc 29 §3 |
| [27-presentation-loop.md](27-presentation-loop.md) | UI loop end-to-end; aligned with doc 29 (typed templates, service publication) |
| [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) | Pile Counter sample; rule rewritten under doc 29's shape (services injected, typed templates) |
| [25-gamerules-shape.md](25-gamerules-shape.md) | `GameRules` interface — abstract class with injected services; `OnQueueDrained` returns Dispatch only |
| [24-continuous-ongoing-effects.md](24-continuous-ongoing-effects.md) | Attachment entity model, turn signals, cleanup |
| [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) | `ITriggerPipeline`, `FilterTriggers`, replacement/prevention, priority, async |
| [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) | Layer hierarchy, two dispatch paths, async model — feeds task #20 |
| [21-trigger-reaction-system.md](21-trigger-reaction-system.md) | Trigger fold, subscription, OnBefore/OnAfter |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | End-to-end flow |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | CompositeAction, scope helpers — feeds task #20 |
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
