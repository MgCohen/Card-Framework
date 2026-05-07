---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on **triggers + dispatch flow** + the runtime-contract reshape that preceded it — closed and **patch cascade landed** 2026-05-07 across 7 commits ([PR #104](https://github.com/MgCohen/Card-Framework/pull/104)).

Net architectural moves now in canonical Models:

- **Three-level model named.** Commands (atomic, framework-FIFO) / Effects (multi-command logic units, game-scheduled) / Triggers ⊆ Effects (reactive effects with intercepted-command payload). Effects and triggers are internally identical.
- **Two-pipeline architecture.** Command pipeline (framework, strict FIFO, fold-based per-command primitives) + Effect pipeline (game-customizable, decides when each effect *starts*). Per-command primitives — `scope.Modify` / `scope.Cancel` / `scope.Replace` / `scope.Dispatch` — stay framework-level; **no game override on command-queue routing**.
- **Runtime contract reshape.** Try-get `Dispatch(GameAction, out string? reason)` and matching lifecycle methods (`StartMatch`, `FinishMatch`); two-method runtime contract (`FilterTriggers` + `Evaluate`); snapshot publication purely runtime-driven via `Actions.Set` from inside `Evaluate`. Deletions: `Route` / `OnIncomingCommand`, `IIntakeQueue`, `IntakeDecision`, `IntakeContext`, `IntakeResult`, `DispatchOrigin`, `OnQueueDrained`, `DrainResult`. `rules` → `runtime` rename.
- **B′ rejected** (`Enqueue` / `GetNext` / `EnqueueReaction` / `EnqueueReplacement` on the runtime) — wrong abstraction level. Effect-level orchestration is what games customize.
- **ADR-0006 created** — `decisions/0006-two-pipeline-architecture.md` pins the architecture as a top-level decision. ADR-0001 + ADR-0002 amended for `IIntakeQueue` removal and "zero gameplay state" refresh. ADR-0003 (`OnIncomingCommand` is purely flow) marked superseded.
- **Cascade scope landed.** ADRs (3 files) → engine Models (1, 2, 3, 4, 5, 6) → triggers/01 + effects/01-02-03 → presentation/01 + support/samples/pile-counter → ledgers (GLOSSARY, OPEN-ITEMS) → actions/01-02-actions.md + tail-end ADR amendments. Remaining stale-keyword references are all in historical sections, supersession markers, or amendment text — properly flagged.
- **Open items** — `oq-intake-queue`, `oq-stash-release`, `oq-queue-ordering` resolved by deletion. New: `oq-effect-pipeline` for the next design pass.

Earlier in the broader project: domain modeling design pass + patch cascade **both landed** (2026-05-06). 13 decisions propagated into canonical Models (`engine/07-game-definition`, `players/01-player-model`, `engine/05-game-factory` MatchSetup reshape, `ISliceProvider` adoption across cards/zones/entities, ADR-0005 + glossary + FEATURE-MAP updates, domain-model spike superseded). Plan A/B/C doc cascade landed earlier ([Card-Framework PR #100](https://github.com/MgCohen/Card-Framework/pull/100)).

---

## 🎯 Active focus

**Effect-pipeline design pass** (`oq-effect-pipeline`). The substantive open design surface from ADR-0006: how does the runtime express effect-level orchestration — when each effect *starts*, whether effects nest mid-execution, batch (Hearthstone-style), or sit on a stack zone (MTG-style) until resolved? The mechanism (queue methods? "should-start-now" hook? state-driven via `Evaluate` watching game-state zones?) is the open question. Will touch `effects/01-async-dispatch.md` and `effects/04-effect-flow.md`; possibly also `triggers/01-trigger-fold.md` if the trigger-as-effect framing surfaces new constraints.

Possible alternative threads:
- **Resolve top open items.** `oq-entity-destruction` (cleanup lifecycle), `oq-effect-cancellation-on-match-end` (async cancellation), `oq-attachment-storage` (sub-zone vs registry vs dedicated zone).
- **First vertical-slice prototype.** Build the actual `PileCounterGameDefinition` against the now-settled domain model + runtime contract — would force-test both reshapes end-to-end.
- **Promote a `considered` feature** (`authoring`, `interaction`, `networking`).

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.
- `oq-attachment-storage` — where attachments (equipment, auras, buffs) live: sub-zone, global registry, or dedicated zone.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

*Scratch is empty — domain-modeling thread closed; locked decisions landed in the canonical Models.*

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
