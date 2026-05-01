---
type: checkpoint
schema-version: 2
last-updated: 2026-05-01
head: session-20260501-1132-engine-wiring
merged:
  - session-20260501-2359-cutover
  - session-20260501-1105-engine-wiring
  - session-20260501-1107-zone-topology
  - session-20260501-1237-engine-wiring-stack
  - session-20260501-1132-engine-wiring
batch:
  - session-20260501-1132-engine-wiring
tags: [#checkpoint]
---

# Session Checkpoint

**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

S(engine-wiring 2026-05-01 11:32): Design pass (6 Qs) + patches applied on engine wiring stack — IHandlerRegistry resolver-only, ICommandPipeline owns validate/execute, ITriggerSystem/ITriggerPipeline split, drain loop bug fixed, VContainer wiring, GameEngineBuilder as config object. 4 debt entries opened for cascade cleanup.

---

## 🎯 The active open question (singular)

**Debt discharge (ad-022 through ad-025)** — four cascade entries from the engine wiring session: update `engine/03-snapshot-pipeline` (stale IHandlerRegistry), update ADR-0001 (stale service list), strike through 5 resolved oq-IDs in `OPEN-QUESTIONS.md`, reconcile `ITriggerPipeline` shape between `effects/02` and `triggers/01`.
*(From session-20260501-1132-engine-wiring.)*

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
| **1** | **Debt discharge (ad-022–025)** | Mechanical — engine/03, ADR-0001, OPEN-QUESTIONS.md, ITriggerPipeline shape conflict. Run `/builder:debt-pass`. |
| **2** | **Merge test branches** | `test/parallel-A` and `test/parallel-B` served their purpose — clean up after merge-pass PR merges. |
| **3** | **Step 8 docs** | Update `PRODUCT-BUILDER.md` + `REPORT-STYLE.md` with parallel-session model and merge-pass cadence. |
| **4** | **Zone model internal opens** | Nine sub-questions in `oq-zone-internal-opens` — design-pass candidate. |

---

## Artifacts to read on resume

- [`features/engine/models/01-evaluation-loop.md`](features/engine/models/01-evaluation-loop.md) — ICommandPipeline interface + fixed drain loop
- [`features/engine/models/02-gamerules-contract.md`](features/engine/models/02-gamerules-contract.md) — 3-service GameRules, ICommandHandler/CommandHandler<T,R>, IHandlerRegistry resolver-only
- [`features/triggers/models/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md) — ITriggerSystem/ITriggerPipeline split
- [`ledgers/ALIGNMENT-DEBT.md`](ledgers/ALIGNMENT-DEBT.md) — ad-022 through ad-025

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
