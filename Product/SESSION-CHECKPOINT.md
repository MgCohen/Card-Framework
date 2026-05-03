---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on `oq-engine-match-lifecycle` — 7 decisions locked. Match lifecycle lives on `GameEngine` directly (`StartMatch` / `FinishMatch` / `Lifecycle` / `Dispose`); `StartMatch` flips Built→Started + emits `MatchStartedSignal` (no automatic drain, no bootstrap list); `FinishMatch(MatchOutcome)` is the primitive, framework-owned `EndMatchCommand` + handler delegates to it; `MatchOutcome` is an abstract record with sealed subtypes (`Win`, `Loss`, `Draw`, `Aborted`); on end, queue tail drains naturally and runtime's `OnIncomingCommand` handles any game-specific reject policy; `engine.Dispose()` releases engine-internal holdings only (runtime, internal services) — the per-match DI scope is owned by a higher app/game-flow layer; async-effect cancellation deferred to a follow-up. Patch cascade pending: new `engine/06-match-lifecycle.md`, plus edits to `engine/01`, `engine/02`, `engine/04`, `engine/05`, pile-counter §9.6, glossary, OPEN-ITEMS.

Prior pass on `oq-gamerules-runtime-definition` — patch cascade still pending: `engine/02`, `engine/05`, glossary, pile-counter §9.8, OPEN-ITEMS.

---

## 🎯 Active focus

Patch cascade for two design passes: (1) `oq-engine-match-lifecycle` (new doc + edits across `engine/01`, `engine/02`, `engine/04`, `engine/05`, pile-counter §9.6, glossary, OPEN-ITEMS), (2) `oq-gamerules-runtime-definition` (the prior pending cascade across `engine/02`, `engine/05`, glossary, pile-counter §9.8, OPEN-ITEMS). After both land, `oq-game-definition` (Track B from the engine-builder pass) is the next design thread.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-engine-match-lifecycle` — engine `StartMatch` / `FinishMatch` hooks for symmetric bootstrap/teardown, runtime disposal, end-of-match signal. Surfaced from the gamerules runtime/definition pass.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

*none*

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
