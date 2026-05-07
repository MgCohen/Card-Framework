---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on **triggers + dispatch flow** (and an in-flight runtime-contract reshape that preceded it) — closed 2026-05-07. Net architectural moves:

- **Three-level model named.** Commands (atomic, framework-FIFO) / Effects (multi-command logic units, game-scheduled) / Triggers ⊆ Effects (reactive effects with intercepted-command payload). Effects and triggers are internally identical.
- **Two-pipeline architecture.** Command pipeline (framework, strict FIFO, fold-based per-command primitives) + Effect pipeline (game-customizable, decides when each effect *starts*). Per-command primitives — `scope.Modify` / `scope.Cancel` / `scope.Replace` / `scope.Dispatch` — stay framework-level; **no game override on command-queue routing**.
- **`FilterTriggers` stays on the runtime** (Q1 = A). Game-wide trigger eligibility belongs with other game decisions.
- **B′ rejected** (`Enqueue` / `GetNext` / `EnqueueReaction` / `EnqueueReplacement` on the runtime). Was solving the wrong abstraction level — what games actually customize is effect-level orchestration, not command queue routing.
- **In-flight runtime-contract reshape** (preceded this design pass; not yet patched): try-get `Dispatch(GameAction, out string? reason)` and matching lifecycle methods; deletions of `Route` / `OnIncomingCommand` / `IIntakeQueue` / `IntakeDecision` / `IntakeContext` / `IntakeResult` / `DispatchOrigin` / `OnQueueDrained` / `DrainResult`; snapshot publication purely runtime-driven via `Actions.Set`; ADR-0002 refresh to "zero **gameplay** state"; `rules` → `runtime` rename.
- Patch cascade pending — land both reshapes together as one cascade. Followed by a focused design pass on the effect-pipeline shape.

Earlier in the broader project: domain modeling design pass + patch cascade **both landed** (2026-05-06). 13 decisions propagated into canonical Models (`engine/07-game-definition`, `players/01-player-model`, `engine/05-game-factory` MatchSetup reshape, `ISliceProvider` adoption across cards/zones/entities, ADR-0005 + glossary + FEATURE-MAP updates, domain-model spike superseded). Plan A/B/C doc cascade landed earlier ([Card-Framework PR #100](https://github.com/MgCohen/Card-Framework/pull/100)).

---

## 🎯 Active focus

**Patch cascade for triggers+dispatch design pass + runtime-contract reshape.** Land the two together as one cascade so the docs aren't internally inconsistent mid-flight:

- `engine/02-gamerules-contract.md` — affirm `FilterTriggers` location; remove candidate `Enqueue` / `GetNext` (B′ retraction); document command-vs-effect-pipeline split; switch surface to try-get `Dispatch`.
- `engine/01-evaluation-loop.md` — pending list stays engine-private FIFO; `bool Dispatch(GameAction, out string? reason)`; `rules` → `runtime` rename; remove `OnQueueDrained` / `DrainResult` references; remove `OnIncomingCommand` / `Route`.
- New ADR `decisions/0006-two-pipeline-architecture.md` — name the three-level model and command/effect pipeline split.
- ADR-0002 (`gamerules-zero-state`) — refresh wording to "zero **gameplay** state."
- `triggers/01-trigger-fold.md` — align with try-get / runtime rename / removal of `OnIncomingCommand`.
- `effects/02-replacement-prevention.md` — align with the runtime contract changes.
- `effects/01-async-dispatch.md` + `effects/04-effect-flow.md` — align with three-level model + new effect-pipeline framing (light touch; deeper work is the next design pass).
- `presentation/01-presentation-loop.md` + `support/samples/pile-counter.md` — update `engine.Dispatch` call sites to the new try-get shape.
- `ledgers/OPEN-ITEMS.md` — resolve `oq-intake-queue` and `oq-stash-release` by deletion; add `oq-effect-pipeline`.
- `ledgers/GLOSSARY.md` — update `Trigger` / `Effect` entries to reflect "triggers ⊆ effects"; add command/effect pipeline entries.

Once the cascade lands, the next thread is the **effect-pipeline design pass** — substantive enough to warrant its own walk, will touch async-dispatch and effect-flow Models.

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
