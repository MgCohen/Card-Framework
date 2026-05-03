---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on `oq-gamerules-runtime-definition` — 4 decisions locked. Split `GameRules` into `GameRulesDefinition` (pure `[SerializeField]` data) + `GameRulesRuntime<TDefinition>` (stateful, DI-resolved, holds services + game subsystems). Factory uses container open-generic resolution. Single-phase construction with typed `Definition` on the base. Per-match lifecycle, engine-managed disposal. Patch cascade pending: `engine/02`, `engine/05`, glossary, pile-counter §9.8, OPEN-ITEMS.

Doc-workflow simplification rollout fully landed earlier; `oq-engine-builder` design pass landed `engine/05-game-factory.md`.

---

## 🎯 Active focus

Patch cascade for the `oq-gamerules-runtime-definition` design pass — apply the runtime/definition split across `engine/02`, `engine/05`, glossary, pile-counter §9.8, and OPEN-ITEMS. Then `oq-game-definition` (Track B from the engine-builder pass) is the next natural design thread.

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
