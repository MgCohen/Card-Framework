# Research session checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-26 (session end; refresh when you read this in a new session)

---

## Where we stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only (see root `AGENTS.md`).
- **Core model, zones, and action vocabulary** are largely written up.
- **Action flow** is described: request → `GameCommand` → pipeline → mutations / semantics → `HistoryEnvelope` → UI.
- **Engine model** is in [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md): `GameEngine` drain loop, `ICommandPipeline` (one command), `IEngineEvaluator` (state-based follow-ups and **queue-drained** hook).
- **Naming (done this session in doc 20):** the lifecycle hook is **`OnQueueDrained`**, not `OnIdle` — it runs when a `Drain` pass finishes and the work queue is empty, not as a generic "application idle" concept.

### Design notes captured in conversation (not all in 20 yet)

- **Wiring direction:** entry points (RequestGateway, timer, triggers) should **hold and call** the engine (e.g. `Dispatch`); the engine should **not** own concrete services. A **composition root** (or game preset factory) can still create engine + services and wire them in one place.
- **Output from engine:** the engine may need to push **available actions, phase/lock, envelopes** to UI through narrow abstractions (e.g. publishers), separate from the command input path. Worth a short subsection when 20 is expanded.

### Artifacts to read on resume

| File | Why |
|------|-----|
| [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) | Current engine + evaluator + `OnQueueDrained` |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | **Still** uses old `RunReactions` + `SamplePlayThenExecuteEngine` — needs alignment to 20 |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | Composites, segments; still good |
| [06-open-questions.md](06-open-questions.md) | Triage after 20 |

---

## Suggested next steps (next session)

1. **Align [18-action-flow-snippets.md](18-action-flow-snippets.md)** with [20](20-engine-evaluation-loop.md) — remove inline `RunReactions` from the dispatcher, replace section 6b with engine + `IEngineEvaluator` + `OnQueueDrained`. Largest doc continuity gap.
2. **Optionally add to doc 20** — composition root sketch; output channel (`IAvailableActionsPublisher` / history); one more evaluator example (deeper priority).
3. **Triage [06-open-questions.md](06-open-questions.md)** — close or promote what 08–20 cover; add items (command naming, evaluator modules, queue ordering, engine output abstractions).
4. **Code milestone** — when ready, add `PLANS/` for "minimal `GameEngine` + pipeline + one evaluator" and link from `AGENTS.md` if it becomes policy.

---

## How to use this file

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
