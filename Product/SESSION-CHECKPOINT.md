---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Mid-rollout of the doc-workflow simplification (`PLANS/simplify-doc-workflow.md`). Six PRs in flight (front-matter rename → ALIGNMENT-DEBT retirement → ledger consolidation → session lightening → template + linter + per-feature ADR rollout → root-doc refresh).

---

## 🎯 Active focus

Land the simplification cleanup PRs in order, then resume design work on the active open items below.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The two highest-leverage durable items right now:

- `oq-engine-builder` — `GameEngineBuilder` construction protocol. Load-bearing for any game author.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

- *(none right now)*

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
