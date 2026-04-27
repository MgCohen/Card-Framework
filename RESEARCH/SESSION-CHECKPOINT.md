# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-27

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Docs 14–20 are now aligned.** The `CommandReaction` inline pipeline step has been removed from docs 15 and 17 and replaced with the evaluator pattern from doc 20. `childEnvelopes` removed from envelope shapes.
- **`RequestAcknowledgement` renamed back to `RequestResult`** in doc 18 — simpler shape, no static factory methods.
- **`OriginIntentId` renamed to `OriginCommandId`** throughout docs 18 and 20. `OriginIntent` added to `GameCommand` and `EnvelopeCause` (non-null only on the first command in a chain).
- **Available actions system documented** in doc 20 section 10 — optional, no-op by default, published via `OnQueueDrained`, read by gateway for fast-reject.
- **Doc 06 triaged** — scheduler questions closed, new open items added (async input suspension, available actions shape, IGameState contract).

### Key decisions settled this session

- `OriginCommandId` is the chain ID, not the request ID. Engine threads it onto all evaluator follow-ups mechanically.
- `OriginIntent` is carried by the first envelope only, for UI/debug correlation.
- `IAvailableActionsSnapshot` is optional — Stacklands-style games don't publish one; all requests pass.
- `CommandReaction` as a pipeline step is gone. Follow-ups come from `IEngineEvaluator.Evaluate`.

### Terminology decisions (2026-04-27)

- `Request` → `PlayerIntent`. Covers all player-originated inputs regardless of mechanism. Not "a request for permission" — an expression of intent.
- `RequestResult` → `IntentResult`.
- All ID types → `Identifier<T>` base record. No custom ID type per concept.
- `IAvailableActionMatcher` → `IntentMapper`. Provisional — the full intent/available-action/validation relationship is a deferred design topic.
- `IEngineEvaluator` → `GameRules`. The engine is the framework loop; `GameRules` is the game-specific plugin.
- `IGameState` → `GameState`. Drop the `I` prefix.
- `CommandReaction` removed from glossary. Superseded by `GameRules`.
- Added to glossary: `StateChange`, `SemanticEvent`, `ResolutionScope`, `EnvelopeCause`, `AvailableActionsSnapshot`, `RelocateInstanceMutation`, `IntentMapper`.

### Open design notes (not yet in any doc)

- **Effects need async support** — pure logic is sync today, but player-input effects (targeting, choices, mulligan) require suspension. How the pipeline pauses and resumes is unresolved.
- **Pre-hook system is a placeholder** — a proper trigger and reaction system is needed; `CommandPreHook` is too blunt.
- **`IGameState` contract is vague** — what it exposes to the evaluator, what is read-only, and where its boundaries are has not been written down.
- **Doc 19 composite actions not leveraged** — scope utilities exist in doc 19 but no handler uses them yet.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| ~~1~~ | ~~**Terminology review**~~ | Done. Glossary updated; renames captured above. Docs 17/18/20 still use old names — update on next pass. |
| 2 | **`GameState` contract** | Write what `GameRules` can and cannot see; read-only boundary |
| 3 | **Redesign pre-hook → trigger/reaction system** | Replace `CommandPreHook` placeholder with a proper model |
| 4 | **Refine `GameRules` / engine shape** | Identify missing hooks beyond `Evaluate` and `OnQueueDrained` |
| ~~5~~ | ~~**Leverage doc 19 composite actions**~~ | Done. New section 5 in doc 19: `MoveCard` composite with before/after for `PlayCardHandler` and `ExecuteCardHandler`. |
| 6 | **Rules and scheduling research** | Replacement/prevention effects, simultaneous events, continuous effects, async input suspension |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice: engine + pipeline + `GameRules` + one card type |
| 8 | **Expand available actions system** | Flesh out shape, lifecycle, and interaction with `IntentMapper`; cover non-trivial filtering cases |
| 9 | **Revisit `StateChange` and state overall** | Clarify what a `StateChange` is, what it owns, how `GameState` exposes/mutates it, and read-only boundaries |
| 10 | **Revisit `SemanticEvent` and scope actions** | Enumerate what a `ResolutionScope` can emit/do; relationship between semantic events, history, and presentation hooks |

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | Primary code-level reference; most up to date |
| [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) | Engine loop, evaluator, available actions |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | Composite actions and scope utilities — not yet leveraged |
| [06-open-questions.md](06-open-questions.md) | Current open question state |

---

## How To Use This File

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
