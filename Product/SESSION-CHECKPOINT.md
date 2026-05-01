---
type: checkpoint
schema-version: 2
last-updated: 2026-05-01
head: session-20260501-1528-debt-pass
merged:
  - session-20260501-2359-cutover
  - session-20260501-1105-engine-wiring
  - session-20260501-1107-zone-topology
  - session-20260501-1237-engine-wiring-stack
  - session-20260501-1132-engine-wiring
  - session-20260501-1528-debt-pass
batch:
  - session-20260501-1528-debt-pass
tags: [#checkpoint]
---

# Session Checkpoint

**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- S(debt-pass 2026-05-01 15:28): Auto debt-pass: 14 entries discharged (broken path references removed from 5 docs), 0 stragglers caught, 1 skipped (ITriggerPipeline reconciliation needs session).

---

## 🎯 The active open question (singular)

**Discharge `ad-20260501-025`** — reconcile `ITriggerPipeline` shape between `effects/02` and `triggers/01`: `effects/02 §3` has `Task<TriggerFoldResult> RunBefore(GameCommand, IReadOnlyList<TriggerMatch>, IEffectScope)`; `triggers/01 §7` has `BeforeFoldResult RunBefore(GameAction, IEffectScope)`. Pick one authoritative shape and patch the other doc.
*(From session-20260501-1528-debt-pass.)*

---

## ❓ Other still-open

- id: oq-entity-destruction | **Entity destruction / Cleanup lifecycle** — what "destroyed" means for the Store, when the framework Cleanup hook fires relative to the destruction command envelope, and what happens to in-flight dispatches referencing the destroyed entity. Load-bearing for ongoing effects that self-destruct.
- id: oq-entity-modifier-api | **Entity modifier mutation API (Scaffold bridge)** — concrete payload shapes for `AddModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload` through the `com.scaffold.entities.states` bridge.
- id: oq-zone-internal-opens | **Zone model internal open questions** — nine questions within `zones/01-zone-model.md`: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment.
- id: oq-attachment-storage | **Attachment storage location** — whether attachments live in a sub-zone on the host, a global attachment registry, or a dedicated zone.
- id: oq-stash-release | **Stash release mechanics** — MTG-style scheduling `Stash`; release mechanism, ordering rules, LIFO ordering not designed.
- id: oq-queue-ordering | **Queue ordering shape** — FIFO queue shown; priority queues, interrupt injection, or stack semantics via engine or `GameRules` unresolved.
- id: oq-gamerules-composition | **`GameRules` composition** — single implementation shown; composition contract (provider/policy ordering, enable/disable) undesigned.
- id: oq-intake-queue | **`IIntakeQueue` shape** — same operations as original `LinkedList`-typed `Pending`; `CurrentlyExecuting` placement unresolved.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **1** | **Discharge `ad-20260501-025`** | ITriggerPipeline shape conflict — `effects/02 §3` vs `triggers/01 §7`. Design-pass candidate. |
| **2** | **Zone model internal opens** | Nine sub-questions in `oq-zone-internal-opens` — design-pass candidate. |
| **3** | **`GameEngineBuilder` construction protocol** | `oq-engine-builder` — load-bearing for any game author. |
| **4** | **Step 8 docs** | Update `PRODUCT-BUILDER.md` + `REPORT-STYLE.md` with parallel-session model and merge-pass cadence. |

---

## Artifacts to read on resume

- [`features/triggers/models/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md) — ITriggerPipeline subscription surface (ad-025 source)
- [`features/effects/models/02-replacement-prevention.md`](features/effects/models/02-replacement-prevention.md) — ITriggerPipeline execution surface (ad-025 target)
- [`ledgers/ALIGNMENT-DEBT.md`](ledgers/ALIGNMENT-DEBT.md) — ad-025 still open

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
