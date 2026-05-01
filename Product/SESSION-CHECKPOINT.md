---
type: checkpoint
schema-version: 2
last-updated: 2026-05-01
head: session-20260501-1107-zone-topology
merged:
  - session-20260501-2359-cutover
  - session-20260501-1105-engine-wiring
  - session-20260501-1107-zone-topology
batch:
  - session-20260501-1105-engine-wiring
  - session-20260501-1107-zone-topology
tags: [#checkpoint]
---

# Session Checkpoint

**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- S(engine-wiring 2026-05-01 11:05): Live test of v2 start-session + end-session branch mode on test/parallel-A — no content work; schema validation only.
- S(zone-topology 2026-05-01 11:07): Live test of v2 start-session + end-session branch mode on test/parallel-B — no content work; schema validation only.

---

## 🎯 The active open question (singular)

**Engine wiring stack** — `IHandlerRegistry` interface, `GameEngineBuilder` construction protocol, `ICommandPipeline` interface, engine startup / first `OnQueueDrained` trigger, `GameRules` service wiring. Cluster of 5 — one design-pass session.
*(From session-20260501-1107-zone-topology.)*

---

## Recent exit questions (parallel batch)

- **Engine wiring stack** — `IHandlerRegistry` interface, `GameEngineBuilder` construction protocol, `ICommandPipeline` interface, engine startup / first `OnQueueDrained` trigger, `GameRules` service wiring. — from `session-20260501-1105-engine-wiring`.

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
| **1** | **Engine wiring design pass** | `IHandlerRegistry` + `GameEngineBuilder` + `ICommandPipeline` + `ITriggerSystem`/`ITriggerPipeline` unification + startup. Cluster of 5 — one design-pass session. |
| **2** | **Merge test branches** | `test/parallel-A` and `test/parallel-B` served their purpose — clean up after merge-pass PR merges. |
| **3** | **Step 8 docs** | Update `PRODUCT-BUILDER.md` + `REPORT-STYLE.md` with parallel-session model and merge-pass cadence. |

---

## Artifacts to read on resume

_(none — test sessions had no Product/features or Product/ledgers changes)_

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
