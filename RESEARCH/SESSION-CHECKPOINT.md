# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-27 (session 5)

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 23 written and amended** — replacement and prevention effects. `ITriggerPipeline` interface introduced (async). `GameRules.FilterTriggers` defined. Priority (`int priority`, lower first). Simultaneous events: sequential only; true simultaneity is explicitly off the table.
- **Doc 24 written** — continuous and ongoing effects. Attachment entity model: create, subscribe to turn/game signals, tick via variables, self-destruct, framework Cleanup on destruction auto-unsubscribes.
- **Full cross-document audit completed** — inconsistencies and stale content identified and fixed across docs 09, 13, 15, 16, 17.
- **Docs 18 and 19 snippet-fixed** — all stale code in both documents updated to match current design: async `IEffect.Execute(IEffectScope)`, async `CommandPipeline.Execute`, `ITriggerPipeline.RunBefore/RunAfter` with `FilterTriggers` + `available` list, `IEffectScope` replacing `EffectContext`, `scope.Dispatch` producing sibling envelopes replacing `scope.DispatchChild`, `ResolutionScope` no longer has `DispatchChild`. Status notes removed from both docs.

### Key decisions settled this session

- **Three before-fold outcomes:** modification, cancellation, substitution — all via `FoldResult` discriminated union (`Keep`, `Cancel`, `Replace`).
- **`ITriggerPipeline` async:** `RunBefore`/`RunAfter` return `Task`; `CommandPipeline.Execute` is `async Task<CommandResult>`. Default impl completes synchronously.
- **Priority on registration:** `int priority = 0`; lower runs first; stable sort. Swappable via custom `ITriggerPipeline`.
- **`IEffectScope` is the Effect's dispatch handle:** `Task<CommandResult> Dispatch(GameAction action)` — immediate inline execution, own sibling envelope, not folded into calling handler's envelope.
- **`scope.DispatchChild` removed.** Effects call `scope.Dispatch`; sibling envelopes share `OriginCommandId`.
- **Replacement-of-replacements is free.** Fold evaluates each trigger's condition against the *current* command after prior transforms. No loop detection needed.
- **Attachments are entities.** Anything with variables is an entity; ongoing effects = attachment entities subscribing to `GameSignal`s.

### Still open

- **`GameState` contract** — read-only boundary for `GameRules` and trigger `Context`. Blocked on canonical state decision.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions.
- **`GameRules` hook completeness** — verify no missing engine seams before implementation.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| ~~1~~ | ~~**Terminology review**~~ | Done. |
| 2 | **`GameState` contract** | Blocked on canonical state (being resolved elsewhere). |
| ~~3~~ | ~~**Redesign pre-hook → trigger/reaction system**~~ | Done. Doc 21. |
| 4 | **Refine `GameRules` / engine shape** | Verify no missing hooks; evaluator composition. |
| ~~5~~ | ~~**Leverage doc 19 composite actions**~~ | Done. |
| ~~6a~~ | ~~**Replacement/prevention effects**~~ | Done. Doc 23. |
| ~~6b~~ | ~~**Simultaneous events**~~ | Done. Sequential in defined order; true simultaneity off the table. |
| ~~6c~~ | ~~**Continuous / ongoing effects**~~ | Done. Doc 24. |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice: engine + pipeline + `GameRules` + one card type |
| 8 | **Expand available actions system** | Shape, lifecycle, interaction with `IntentMapper` |
| 9 | **Revisit `StateChange` and state overall** | Blocked on canonical state. |
| ~~10~~ | ~~**Revisit `SemanticEvent` and scope actions**~~ | Done. |
| ~~11~~ | ~~**Align docs 18–20 with new naming + fix stale snippets**~~ | Done. Audit + snippet fixes in docs 18, 19. |
| 12 | **Game lifecycle design** | Session start, turn structure, phase transitions, end conditions — prerequisite for subscription lifecycle |
| ~~13~~ | ~~**Trigger ordering / priority weight on registration**~~ | Done. `int priority = 0`; lower first; stable sort. (doc 23) |
| ~~14~~ | ~~**`ITriggerPipeline` async**~~ | Done. `RunBefore`/`RunAfter` return `Task`. (doc 23) |

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [24-continuous-ongoing-effects.md](24-continuous-ongoing-effects.md) | Attachment entity model, turn signals, cleanup — most recent doc |
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
