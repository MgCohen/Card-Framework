# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-28 (session 6)

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 25 written + revised** — `GameRules` interface locked: four sync methods (`OnIncomingCommand`, `FilterTriggers`, `Evaluate`, `OnQueueDrained`). §3 reworked: dropped `SchedulingDecision`/`SchedulingKind` enum in favor of an `ICommandScheduler` handle + minimal `IntakeResult(Accepted, Reason?)`. Three rejection paths canonicalized (AvailableActions / Intake Reject / Before-fold Cancel). Anti-scope and composition pattern explicit. Closes checkpoint item #4.
- **Doc 24 written** — continuous and ongoing effects. Attachment entity model: create, subscribe to turn/game signals, tick via variables, self-destruct, framework Cleanup on destruction auto-unsubscribes.
- **Doc 23 written and amended** — replacement and prevention effects. `ITriggerPipeline` interface introduced (async). `GameRules.FilterTriggers` defined. Priority (`int priority`, lower first). Simultaneous events: sequential only; true simultaneity is explicitly off the table.
- **Full cross-document audit completed** — inconsistencies and stale content identified and fixed across docs 09, 13, 15, 16, 17.
- **Docs 18 and 19 snippet-fixed** — all stale code in both documents updated to match current design: async `IEffect.Execute(IEffectScope)`, async `CommandPipeline.Execute`, `ITriggerPipeline.RunBefore/RunAfter` with `FilterTriggers` + `available` list, `IEffectScope` replacing `EffectContext`, `scope.Dispatch` producing sibling envelopes replacing `scope.DispatchChild`, `ResolutionScope` no longer has `DispatchChild`. Status notes removed from both docs.

### Key decisions settled this session

- **`GameRules` is a sync decision layer; only intake is a controller.** `FilterTriggers`/`Evaluate`/`OnQueueDrained` are observers. `OnIncomingCommand` is given an `ICommandScheduler` because scheduling is its primary action. None of the methods mutate `GameState`.
- **`GameRules` may hold internal state** — rule modules, caches, stash buffers. Just not *game* state.
- **Three rejection paths canonical:** AvailableActions (gateway, pre-dispatch, `PlayerIntent` only) / Intake `Reject` (`OnIncomingCommand` returns `IntakeResult.Reject(reason)`) / Before-fold `Cancel` (`OnBefore<T>` trigger). Earliest path with enough info wins.
- **`OnIncomingCommand` is universal intake.** Every dispatched command — player, timer, trigger reaction, internal — passes through it. Rule calls `scheduler.Enqueue` / `scheduler.PushFront`, stashes internally, or rejects. No fixed enum of behaviors; framework provides no stash primitive.
- **`Evaluate` is state-based, not type-switched.** Reading from `GameState`; `justExecuted`/`result` are for logging/tie-breaking only. Switch-on-action-type is a smell.
- **`OnQueueDrained` is the only method that holds `GameEngine`.** Other three take state + arguments only. Drain re-enters if the hook dispatches.
- **MTG-strict SBA ordering is a known semantic difference.** Framework runs after-triggers before `Evaluate`; MTG-strict games encode SBAs inside `Evaluate` or accept the timing.

### Still open

- **`GameState` contract** — read-only boundary for `GameRules` and trigger `Context`. Blocked on canonical state decision.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle (#12).
- **`SchedulingContext.Pending` shape** — snapshot vs. live view vs. count. Resolve at engine implementation.
- **`IntakeResult.Reason`** — free-form string vs. structured enum. Defer until a real game forces it.
- **Intake telemetry** — engine sees only `Accepted`/`Rejected`; whether it should observe scheduler calls (for replay/logging) is open.
- **`AvailableActionsSnapshot` ownership** — `GameRules` publishing method vs. state artifact written in `OnQueueDrained`. Pin during #8.
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
| 8 | **Expand available actions system** | Shape, lifecycle, interaction with `IntentMapper`. Also pins `AvailableActionsSnapshot` ownership (open from doc 25). |
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
| [25-gamerules-shape.md](25-gamerules-shape.md) | `GameRules` interface, four methods, three rejection paths, anti-scope — most recent doc |
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
