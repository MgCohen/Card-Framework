---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on `oq-engine-match-lifecycle` landed — new `engine/06-match-lifecycle.md` written; cascade applied across `engine/01` (Dispatch lifecycle gate), `engine/02` (session-lifecycle framing flipped, `EndGameCommand` → `EndMatchCommand`, lifecycle open item moved to Resolved), `engine/03` (sample uses `EndMatchCommand` + `MatchOutcome.Win`), `engine/04` (`EngineState.Lifecycle` field), `engine/05` (return state, scope-ownership softened, Inv-9 narrowed, open follow-ups cleaned), pile-counter (`EndMatchCommand` rename, runtime takes `actors` ctor param, Q3 struck), glossary (5 new entries: `EngineLifecycle`, `MatchOutcome`, `StartMatchCommand`, `EndMatchCommand`, `MatchStartedSignal`, `MatchEndedSignal`), OPEN-ITEMS (`oq-engine-match-lifecycle` struck; `oq-effect-cancellation-on-match-end` added). Prior `oq-gamerules-runtime-definition` cascade also fully landed (audit found it was already mostly applied; only the lifecycle-overlap renames were outstanding, now done).

Prior pass on `oq-gamerules-runtime-definition` — patch cascade still pending: `engine/02`, `engine/05`, glossary, pile-counter §9.8, OPEN-ITEMS.

---

## 🎯 Active focus

`oq-game-definition` (Track B from the engine-builder pass) — `GameDefinition` bundle + definition-declared slice schema. Next design thread now that the lifecycle and runtime/definition cascades have landed.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.

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
