---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Workflow drift cleanup sweep landed (PR #92, merged). Six commits propagated the doc-workflow simplification rollout into the canonical workflow doc, templates, and builder skill files; rewrote the `_templates/checkpoint.md` shape to match what we actually maintain; populated `Product/_workflow/retired-tokens.json` with 15 high-confidence tokens for `/builder:lint` T1; and restored the rich chat-render shape in `start-session.md` + `end-session.md` (the regression PR 5b introduced when it stripped REPORT-STYLE templates alongside the lifecycle machinery). Per-bullet keep/re-source/drop mapping for the chat-template restoration is in commit 6's body. Prior design work — `oq-engine-builder` two-layer split (DI installer + `IGameFactory`) and `engine/05-game-factory.md` — remains the most recent design move.

---

## 🎯 Active focus

*Between threads.* Workflow drift sweep just landed; design-side `oq-game-definition` (definition-declared slice schema + `GameDefinition` bundle) is queued as the next natural pass. `oq-gamerules-runtime-definition` is a smaller follow-up that could land first if the goal is to clear a known incompatibility before more weight piles on `engine/05`.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-gamerules-runtime-definition` — reconcile `GameRules` constructor-injected service shape (engine/02) with serializable-data shape (engine/05). Tightly scoped follow-up from the engine-builder pass.
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
