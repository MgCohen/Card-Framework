# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-28 (session 6)

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 27 written** — presentation/UI loop end-to-end. Resolved `AvailableActionsSnapshot` ownership: lives in `GameState.AvailableActions: IReadOnlyList<GameCommand>`. `AffordanceMap` is the game-specific UI projection (built from the snapshot). Validation has four layers: AffordanceMap (prophylactic) / IntentMapper (well-formedness) / `handler.Validate` (authoritative) / `rule.OnIncomingCommand` (engine flow). `OnQueueDrained` returns a `DrainResult { Snapshot, Dispatch }` rather than dispatching directly — supersedes doc 25 §6.
- **Doc 26 written** — Pile Counter reference sample game. Deliberately the floor: 12 cards, 4 piles, one move action, no triggers, no async, state-based win check. Used as the working reference for sketching framework pieces.
- **Doc 25 written + revised** — `GameRules` interface locked: four sync methods (`OnIncomingCommand`, `FilterTriggers`, `Evaluate`, `OnQueueDrained`). §3 reworked: dropped `SchedulingDecision`/`SchedulingKind` enum in favor of an `ICommandScheduler` handle + minimal `IntakeResult(Accepted, Reason?)`. Three rejection paths canonicalized (AvailableActions / Intake Reject / Before-fold Cancel). Anti-scope and composition pattern explicit. Closes checkpoint item #4.
- **Doc 24 written** — continuous and ongoing effects. Attachment entity model: create, subscribe to turn/game signals, tick via variables, self-destruct, framework Cleanup on destruction auto-unsubscribes.
- **Doc 23 written and amended** — replacement and prevention effects. `ITriggerPipeline` interface introduced (async). `GameRules.FilterTriggers` defined. Priority (`int priority`, lower first). Simultaneous events: sequential only; true simultaneity is explicitly off the table.
- **Full cross-document audit completed** — inconsistencies and stale content identified and fixed across docs 09, 13, 15, 16, 17.
- **Docs 18 and 19 snippet-fixed** — all stale code in both documents updated to match current design: async `IEffect.Execute(IEffectScope)`, async `CommandPipeline.Execute`, `ITriggerPipeline.RunBefore/RunAfter` with `FilterTriggers` + `available` list, `IEffectScope` replacing `EffectContext`, `scope.Dispatch` producing sibling envelopes replacing `scope.DispatchChild`, `ResolutionScope` no longer has `DispatchChild`. Status notes removed from both docs.

### Key decisions settled this session

- **`GameRules` is a sync decision layer.** None of the methods mutate `GameState` directly. Three are pure observers; intake (`OnIncomingCommand`) is the only one with side-effecting authority (queue/stash routing).
- **`GameRules` holds zero game-affecting state.** Locked as contract: rule may hold *config* (modules, caches, lookup tables), never gameplay memory. Anything game-affecting (e.g., Pile Counter's `LastMove`) lives in `GameState`.
- **`OnIncomingCommand` is purely flow.** Pause/ended gating + scheduling routing only. Never reads command body. Command-specific legality lives in `handler.Validate`.
- **Validation moved from rule to handler.** `ICommandHandler<T,R>.Validate(cmd, state, out reason) → bool`. Pipeline runs validate before intake routing; `OnQueueDrained` snapshot builder calls validate per candidate.
- **Generic primitive + rule-enforced constraint pattern.** Framework ships generic verbs (`MoveCardCommand` not `MoveTopCardCommand`); games layer constraints in `Validate`.
- **Player input layer:** `PlayerIntent` (UI) → `IIntentMapper` (game-specific) → `GameCommand` (engine). Three layers explicit.
- **Snapshot lives in state.** `GameState.AvailableActions: IReadOnlyList<GameCommand>` — canonical, generic, framework-defined. Game-specific `AffordanceMap` is a UI-side projection.
- **`OnQueueDrained` returns `DrainResult { Snapshot, Dispatch }`** rather than dispatching directly. Keeps the rule purely observer-shaped (returns data, never side-effects). Supersedes doc 25 §6.
- **Four rejection paths now:** AvailableActions (UI prophylactic) / `OnIncomingCommand` (engine flow) / `handler.Validate` (command legality, authoritative) / Before-fold `Cancel` (card-effect cancellation).
- **State pattern:** GameState is immutable, redux-like, likely Scaffold State package. State mutators react to commands (e.g., `LastMoveMutator` updates `state.LastMove` whenever `MoveCardCommand` resolves).
- **MTG-strict SBA ordering is a known semantic difference.** Framework runs after-triggers before `Evaluate`; MTG-strict games encode SBAs inside `Evaluate` or accept the timing.

### Still open

- **`GameState` contract** — read-only boundary for `GameRules` and trigger `Context`. Blocked on canonical state decision (likely Scaffold State).
- **Stateless vs stateful rule.** Even with "no game state" locked, the question of whether the rule receives `GameState` as a parameter on every method (including `OnIncomingCommand`) or holds a reference is open. Drives `IntakeContext` shape. Deferred.
- **Action pipeline cleanup.** Need a dedicated doc explicit about: handler lookup, validate, execute, trigger folds. Currently scattered across docs 18/19/22/25. Deferred.
- **`EndGameCommand` semantics.** What does the engine do when it resolves? Bleeds into session lifecycle (#12). Deferred.
- **`AvailableActions` mutator** — likely framework-shipped since the snapshot type is canonical. Pin during implementation.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle (#12).
- **`IntakeContext.Pending` shape** — currently sketched as `LinkedList<GameCommand>` (live, manipulable). Confirm at engine implementation.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces it.
- **Re-validation at dequeue** — validate at intake plus a second time before handler runs (state may have changed). Implementation detail; OK as double-validate for now.
- **`FilterTriggers` default impl** — identity for trigger-less games. Likely abstract base class with virtuals rather than pure interface. Will revisit at final rule shape.
- **`CompositeGameRules` helper** — design when first composed-rules game needs it.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| ~~1~~ | ~~**Terminology review**~~ | Done. |
| 2 | **`GameState` contract** | Blocked on canonical state (being resolved elsewhere). |
| ~~3~~ | ~~**Redesign pre-hook → trigger/reaction system**~~ | Done. Doc 21. |
| ~~4~~ | ~~**Refine `GameRules` / engine shape**~~ | Done. Doc 25. Interface locked; rejection paths canonical; anti-scope explicit. |
| ~~5~~ | ~~**Leverage doc 19 composite actions**~~ | Done. |
| ~~6a~~ | ~~**Replacement/prevention effects**~~ | Done. Doc 23. |
| ~~6b~~ | ~~**Simultaneous events**~~ | Done. Sequential in defined order; true simultaneity off the table. |
| ~~6c~~ | ~~**Continuous / ongoing effects**~~ | Done. Doc 24. |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice: engine + pipeline + `GameRules` + one card type. Pull after #12 / #8 land. |
| ~~8~~ | ~~**Expand available actions system**~~ | Done. Doc 27. Snapshot in state; `AffordanceMap` UI projection; four-layer validation; `OnQueueDrained` returns `DrainResult`. |
| 16 | **Action pipeline cleanup** | Dedicated doc: explicit handler lookup, validate, execute, trigger folds. Currently scattered across docs 18/19/22/25. |
| 17 | **State pattern decision** | Stateless vs stateful rule re: how `OnIncomingCommand` receives `GameState`. Drives `IntakeContext` shape. |
| 9 | **Revisit `StateChange` and state overall** | Blocked on canonical state. |
| ~~10~~ | ~~**Revisit `SemanticEvent` and scope actions**~~ | Done. |
| ~~11~~ | ~~**Align docs 18–20 with new naming + fix stale snippets**~~ | Done. Audit + snippet fixes in docs 18, 19. |
| 12 | **Game lifecycle design** | Session start, turn structure, phase transitions, end conditions — prerequisite for trigger subscription lifecycle. Best unblocked task next; feeds `OnQueueDrained` use cases and the `PLANS/` slice. |
| ~~13~~ | ~~**Trigger ordering / priority weight on registration**~~ | Done. `int priority = 0`; lower first; stable sort. (doc 23) |
| ~~14~~ | ~~**`ITriggerPipeline` async**~~ | Done. `RunBefore`/`RunAfter` return `Task`. (doc 23) |
| 15 | **MTG-strict SBA ordering** | Decide whether to add a pre-after-fold evaluator hook or document the workaround. Defer until a game needs it. (doc 25 §5) |

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [27-presentation-loop.md](27-presentation-loop.md) | UI loop end-to-end; snapshot-in-state; `AffordanceMap`; four validation layers; `DrainResult` — **most recent** |
| [26-pile-counter-sample-game.md](26-pile-counter-sample-game.md) | Pile Counter sample game spec — used as the working reference |
| [25-gamerules-shape.md](25-gamerules-shape.md) | `GameRules` interface — needs amendment per doc 27 (handler.Validate, DrainResult, four rejection paths) |
| [24-continuous-ongoing-effects.md](24-continuous-ongoing-effects.md) | Attachment entity model, turn signals, cleanup |
| [23-replacement-prevention-effects.md](23-replacement-prevention-effects.md) | `ITriggerPipeline`, `FilterTriggers`, replacement/prevention, priority, async |
| [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) | Layer hierarchy, two dispatch paths, async model |
| [21-trigger-reaction-system.md](21-trigger-reaction-system.md) | Trigger fold, subscription, OnBefore/OnAfter |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | End-to-end flow — now fully updated |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | CompositeAction, scope helpers — now fully updated |
| [06-open-questions.md](06-open-questions.md) | Current open question state |

---

## How To Use This File

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
