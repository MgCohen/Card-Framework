# Research session checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-26

---

## Where we stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only (see root `AGENTS.md`).
- **Core model, zones, and action vocabulary** are largely written up (glossary, decisions, entity/zone model).
- **Action flow** is largely described: request → `GameCommand` → pipeline → `StateMutation` / semantics → `HistoryEnvelope` → UI.
- **Engine / orchestration layer** is now defined in [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md).

### Latest narrative change (engine defined)

- **[20-engine-evaluation-loop.md](20-engine-evaluation-loop.md)** defines the engine as three parts: **`GameEngine`** (the drain loop), **`ICommandPipeline`** (executes one command, unchanged from 18), and **`IEngineEvaluator`** (game-specific rules brain, called after every command).
- Key decision: the evaluator reads **game state** (zone contents, phase, etc.) rather than pattern-matching on command types. This handles any source putting a card in a zone, not just `PlayCardCommand`.
- The inline `RunReactions` that was embedded in the dispatcher in doc 18 is **removed**. Follow-up commands are entirely the evaluator's job.
- **Entry points** (RequestGateway, TimerService, TriggerSystem) all call `engine.Dispatch(command)`. The engine is the single convergence point.
- Traced example: `PlayCardCommand` moves card into `PlayArea` → evaluator sees zone occupied → enqueues `ExecuteCardCommand` → zone clears → evaluator returns empty → `OnIdle` awaits player input.
- MTG-like variation shown: same evaluator interface, priority state conditions added before auto-executing the stack.

### Earlier artifacts worth a skim on resume

- [18-action-flow-snippets.md](18-action-flow-snippets.md) — end-to-end flow; **needs a cleanup pass** to replace section 4's inline `RunReactions` and section 6b's `SamplePlayThenExecuteEngine` with the engine/evaluator pattern from doc 20.
- [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) — composite zone moves, sequential `Apply`, segment boundaries. Still accurate.
- [17-action-definition-proposal.md](17-action-definition-proposal.md) — payloads, results, services, flow graph.
- [06-open-questions.md](06-open-questions.md) — what is still open vs partially answered.
- [13-deckmech-command-pipeline-reference.md](13-deckmech-command-pipeline-reference.md) — prior sample project baseline.

---

## Suggested next steps (for the next session)

1. **Clean up [18-action-flow-snippets.md](18-action-flow-snippets.md)** — Replace section 4 (`GameCommandDispatcher` with inline reactions) and section 6b (`SamplePlayThenExecuteEngine`) to match the engine/evaluator model from doc 20. This is the main continuity gap between the two docs.
2. **Expand doc 20** — Add a second example evaluator (MTG-like with a real priority state machine) and notes on queue ordering variations (FIFO vs stack vs priority queue).
3. **Reread [06-open-questions.md](06-open-questions.md)** — Close or promote items now covered by 08–20; add new items raised by the engine definition (evaluator composition, command naming split, queue re-entry contract).
4. **When leaving research-only** — Add a `PLANS/` entry for the first code milestone (e.g. "minimal engine + pipeline + one evaluator pair") and link from `AGENTS.md`.

---

## How to use this file

- **On milestone / session end:** Edit this file in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, you are duplicating the numbered docs; trim.
- **Optional:** A one-line link in the next PR or commit message: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` for stable architecture once the project is implemented; it is a **spine** for long research threads.
