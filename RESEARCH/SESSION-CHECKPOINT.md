# Research Session Checkpoint

**Purpose:** One short, **replaceable** handoff for the *next* session. Full reasoning stays in the numbered research files; this file is **only** "where we are, what to read, what to do next." Update or overwrite it when a research milestone ends. Do not duplicate the whole glossary here.

**Last updated:** 2026-04-27

---

## Where We Stopped

- **No implementation** in this repo yet: research and outlines under `RESEARCH/` only.
- **Docs 14–20 are now aligned.** The `CommandReaction` inline pipeline step has been removed from docs 15 and 17 and replaced with the evaluator pattern from doc 20. `childEnvelopes` removed from envelope shapes.
- **`RequestAcknowledgement` renamed to `IntentResult`** (via `RequestResult`) in doc 18 — simpler shape, no static factory methods.
- **`OriginIntentId` renamed to `OriginCommandId`** throughout docs 18 and 20. `OriginIntent` added to `GameCommand` and `EnvelopeCause` (non-null only on the first command in a chain).
- **Available actions system documented** in doc 20 section 10 — optional, no-op by default, published via `OnQueueDrained`, read by gateway for fast-reject.
- **Doc 06 triaged** — scheduler questions closed, new open items added (async input suspension, available actions shape, `GameState` contract).
- **Trigger and reaction system designed** — doc 21 written. `CommandPreHook` placeholder replaced with a proper model.

### Key decisions settled this session

- `OriginCommandId` is the chain ID, not the request ID. Engine threads it onto all evaluator follow-ups mechanically.
- `OriginIntent` is carried by the first envelope only, for UI/debug correlation.
- `IAvailableActionsSnapshot` is optional — Stacklands-style games don't publish one; all requests pass.
- `CommandReaction` as a pipeline step is gone. Follow-ups come from `GameRules.Evaluate`.
- `GameCommand` becomes the abstract base for all pipeline-dispatchable types. The old `GameCommand` (executable action) is now `GameAction`. `SemanticEvent` is now `GameSignal`.
- Trigger fold: `OnBefore<T>` / `OnAfter<T>` where `T : GameCommand`. Only `GameAction` is modifiable in the before fold.
- All trigger reactions dispatch through one unified channel. `GameRules.OnIncomingCommand` decides scheduling: `RunNext` / `Enqueue` / `Stash` / `Discard`.
- Trigger subscriptions are per-instance at runtime; definition carries the subscription logic.

### Terminology decisions (2026-04-27)

- `Request` → `PlayerIntent`. Covers all player-originated inputs regardless of mechanism. Not "a request for permission" — an expression of intent.
- `RequestResult` → `IntentResult`.
- All ID types → `Identifier<T>` base record. No custom ID type per concept.
- `IAvailableActionMatcher` → `IntentMapper`. Provisional — the full intent/available-action/validation relationship is a deferred design topic.
- `IEngineEvaluator` → `GameRules`. The engine is the framework loop; `GameRules` is the game-specific plugin.
- `IGameState` → `GameState`. Drop the `I` prefix.
- `CommandReaction` removed from glossary. Superseded by `GameRules`.
- Added to glossary: `StateChange`, `SemanticEvent`, `ResolutionScope`, `EnvelopeCause`, `AvailableActionsSnapshot`, `RelocateInstanceMutation`, `IntentMapper`.
- `GameCommand` (old executable type) → `GameAction`. `GameCommand` is now the abstract base.
- `SemanticEvent` → `GameSignal`. Emitted mid-handler via `scope.Emit`; goes through trigger fold; no handler step.
- `CommandPreHook` removed. Superseded by `OnBefore<T>` in the trigger system.

### Open design notes (not yet in any doc)

- **Effects need async support** — pure logic is sync today, but player-input effects (targeting, choices, mulligan) require suspension. How the pipeline pauses and resumes is unresolved.
- **`GameState` contract is vague** — what it exposes to `GameRules` and trigger `Context`, what is read-only, and where its boundaries are has not been written down.
- **Doc 19 composite actions not leveraged** — scope utilities exist in doc 19 but no handler uses them yet.
- **Trigger subscription lifecycle** — when Register/Unregister is called; what qualifies as "trigger-active" relative to zone state. Tied to GameState contract.
- **Trigger ordering within fold** — what determines sequence when multiple triggers match the same type.

---

## Suggested Next Steps (Next Session)

| # | Task | Notes |
|---|------|-------|
| ~~1~~ | ~~**Terminology review**~~ | Done. Glossary updated; renames captured above. Docs 17/18/20 still use old names — update on next pass. |
| 2 | **`GameState` contract** | Write what `GameRules` and trigger `Context` can and cannot see; read-only boundary |
| ~~3~~ | ~~**Redesign pre-hook → trigger/reaction system**~~ | Done. Doc 21 written. `CommandPreHook` replaced with `OnBefore<T>` / `OnAfter<T>`. `GameRules.OnIncomingCommand` scheduling hook added. |
| 4 | **Refine `GameRules` / engine shape** | `OnIncomingCommand` added; verify no other missing hooks |
| ~~5~~ | ~~**Leverage doc 19 composite actions**~~ | Done. New section 5 in doc 19: `MoveCard` composite with before/after for `PlayCardHandler` and `ExecuteCardHandler`. |
| 6 | **Rules and scheduling research** | Replacement/prevention effects, simultaneous events, continuous effects, async input suspension |
| 7 | **Write `PLANS/` doc** | Minimal implementation slice: engine + pipeline + `GameRules` + one card type |
| 8 | **Expand available actions system** | Flesh out shape, lifecycle, and interaction with `IntentMapper`; cover non-trivial filtering cases |
| 9 | **Revisit `StateChange` and state overall** | Clarify what a `StateChange` is, what it owns, how `GameState` exposes/mutates it, and read-only boundaries |
| ~~10~~ | ~~**Revisit `SemanticEvent` and scope actions**~~ | Done. `SemanticEvent` → `GameSignal`. Emitted via `scope.Emit`; goes through trigger fold; folds into parent envelope. |
| 11 | **Align docs 18–20 with new naming** | `GameAction`, `GameSignal`, `OnBefore`/`OnAfter`, `GameRules.OnIncomingCommand` not yet reflected in older docs |
| 12 | **Trigger subscription lifecycle** | When Register/Unregister is called; zone-based activation; relationship to GameState contract |

---

## Artifacts To Read On Resume

| File | Why |
|------|-----|
| [21-trigger-reaction-system.md](21-trigger-reaction-system.md) | Trigger system design — most recent doc |
| [18-action-flow-snippets.md](18-action-flow-snippets.md) | Primary code-level reference; needs naming update pass |
| [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) | Engine loop, GameRules, available actions; needs `OnIncomingCommand` added |
| [19-handler-mutations-and-semantics.md](19-handler-mutations-and-semantics.md) | Composite actions and scope utilities |
| [06-open-questions.md](06-open-questions.md) | Current open question state |

---

## How To Use This File

- **On milestone / session end:** Edit in place: refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim; detail belongs in numbered `RESEARCH/` files.
- **Optional:** In a commit/PR, one line: "Research checkpoint: `RESEARCH/SESSION-CHECKPOINT.md`."

This is **not** a substitute for `DOCS/` once the project is implemented; it is a **spine** for long research threads.
