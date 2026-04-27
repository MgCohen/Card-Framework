# Research session checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-27 (session end; refresh when you read this in a new session)

---

## Where we stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Doc 18 is now aligned with doc 20** — the old reaction-based model (`RunReactions`, `SamplePlayThenExecuteEngine`, `ICommandReaction`) is gone. Doc 18 now reflects the engine/evaluator/pipeline split.

### What doc 18 now shows

- `RequestGateway` holds `GameEngine`, calls `engine.Dispatch()`, returns `RequestAcknowledgement` (Rejected or Accepted with `RequestId`).
- `GameCommand` base carries `OriginRequestId` (optional). Gateway stamps it; engine threads it onto evaluator follow-ups.
- `ICommandPipeline` handles exactly one command — no reaction step.
- Section 6b: `SampleGameEvaluator : IEngineEvaluator` with zone-check pattern (state-based, not command-type matching).
- **One envelope per pipeline execution.** Child commands via `scope.DispatchChild` fold into the current envelope; evaluator follow-ups each get their own. Both envelopes in the play→execute chain carry the same `OriginRequestId`.

### Open design note captured in conversation (not in any doc yet)

- **Available actions ownership:** the engine (via `OnQueueDrained`) should compute and publish available player actions as a state artifact. The gateway reads that snapshot for its fast-reject check. The gateway does *not* independently reason about validity — it just checks against what the engine published. This changes `IAvailableActionMatcher` from a game-logic component to a simple reader. **Not written into doc 20 yet.**

### Artifacts to read on resume

| File | Why |
|------|-----|
| [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) | Engine loop, evaluator, `OnQueueDrained` — needs available-actions output section |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | Now up to date; good as reference |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | Composites, segments; still good |
| [06-open-questions.md](06-open-questions.md) | Needs triage |

---

## Suggested next steps (next session)

1. **Add available-actions output to doc 20** — `OnQueueDrained` publishes an `IAvailableActionsSnapshot`; `RequestGateway` reads it. Captures the design decision made this session.
2. **Add `OriginRequestId` threading to doc 20's engine loop** — one line in `Drain` to show the engine copying it onto evaluator follow-ups.
3. **Triage [06-open-questions.md](06-open-questions.md)** — close what 18–20 now cover; add new items (available-actions ownership, envelope correlation, `OriginRequestId` threading).
4. **Code milestone** — when ready, add `PLANS/` for "minimal `GameEngine` + pipeline + one evaluator" and link from `AGENTS.md`.

---

## How to use this file

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
