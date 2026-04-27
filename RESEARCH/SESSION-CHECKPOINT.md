# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-27 (session 3)

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 06 fully audited** — every question cross-checked against docs 08–21. Canonical state (Core Q3) correctly flagged as still open. Five new open items added from docs 20/21. Likely First Decisions items 1/3/4/5 struck through.
- **Doc 22 written** — async decisions and suspension. Layer hierarchy settled. Two dispatch paths defined. Async suspension model resolved.
- **Docs 09 and 21 updated** — `CompositeAction`, `Effect`, `GameAction`, `Decision` glossary entries rewritten to reflect settled hierarchy.

### Key decisions settled this session

- **Layer hierarchy locked:**
  - `Effect` — async orchestrator; the only layer that sequences multiple `GameAction`s; no envelope of its own.
  - `GameAction` — atomic, synchronous handler, single envelope, returns `CommandResult`; never dispatches other `GameAction`s.
  - `CompositeAction` — reusable `StateMutation` group inside a handler; not an orchestrator of commands.
- **Two dispatch paths — not interchangeable:**
  - `engine.Dispatch(action)` — queued, independent, own envelope, processed after current drain step. Used by `GameRules.Evaluate`, trigger reactions, player input.
  - `scope.Dispatch(action)` — executes **immediately and inline**, own envelope published before next line runs, effect awaits result. Used exclusively by effects.
- **Decisions are `GameAction`s.** `RequestTargetAction` is a normal action whose handler opens a decision and returns the chosen value in its `CommandResult`. No special pipeline concept.
- **Async lives only at the effect layer.** Everything returns `Task`; most resolve synchronously (`Task.FromResult`). Those that genuinely need to wait (player input) just wait — no special suspension mechanism in the framework.
- **The engine pauses for the duration of an effect.** While a `scope.Dispatch` is awaited, the engine does not dequeue the next action. Whether this is acceptable is game-specific; `GameRules` controls scheduling.
- **No partial envelopes needed.** Each action completes and publishes its envelope before the next begins. Client always sees fully committed state.

### Still open (not yet addressed this session)

- **`GameState` contract** — what `GameRules` and trigger `Context` can and cannot see; read-only boundary. Blocked on canonical state decision (being resolved elsewhere).
- **Trigger subscription lifecycle** — when Register/Unregister is called; what qualifies as "trigger-active." Deferred to game lifecycle (task 12).
- **Trigger ordering within fold** — sequence when multiple triggers match same type.
- **`GameRules` hook completeness** — verify no missing engine seams before implementation.
- **Replacement/prevention effects** — mechanism exists (`OnBefore<T>`); rules-level semantics (ordering, simultaneous, layers) undesigned.
- **Simultaneous events** — representation and ordering unresolved.
- **Continuous effects / derived values** — recomputation model undesigned.
- **Stash release mechanics** — MTG-style priority stash needs release design.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| ~~1~~ | ~~**Terminology review**~~ | Done. |
| 2 | **`GameState` contract** | Blocked on canonical state (being resolved elsewhere). |
| ~~3~~ | ~~**Redesign pre-hook → trigger/reaction system**~~ | Done. Doc 21. |
| 4 | **Refine `GameRules` / engine shape** | Verify no missing hooks; evaluator composition. |
| ~~5~~ | ~~**Leverage doc 19 composite actions**~~ | Done. |
| 6 | **Rules and scheduling research** | Replacement/prevention, simultaneous events, continuous effects. (Async suspension now resolved — doc 22.) |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice: engine + pipeline + `GameRules` + one card type |
| 8 | **Expand available actions system** | Shape, lifecycle, interaction with `IntentMapper` |
| 9 | **Revisit `StateChange` and state overall** | Blocked on canonical state. |
| ~~10~~ | ~~**Revisit `SemanticEvent` and scope actions**~~ | Done. |
| ~~11~~ | ~~**Align docs 18–20 with new naming**~~ | Done. |
| 12 | **Game lifecycle design** | Session start, turn structure, phase transitions, end conditions — prerequisite for subscription lifecycle |
| 13 | **Continue grilling doc 06 open questions** | Replacement/prevention, simultaneous events, trigger ordering, continuous effects |

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [22-async-decisions-and-suspension.md](22-async-decisions-and-suspension.md) | Layer hierarchy, two dispatch paths, async model — most recent doc |
| [21-trigger-reaction-system.md](21-trigger-reaction-system.md) | Trigger system, updated CompositeAction/Effect distinction |
| [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) | Engine loop, `GameRules`, available actions |
| [09-core-glossary.md](09-core-glossary.md) | Updated Effect, GameAction, CompositeAction, Decision entries |
| [06-open-questions.md](06-open-questions.md) | Current open question state |

---

## How To Use This File

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
