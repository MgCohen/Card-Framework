---
type: checkpoint
schema-version: 2
last-updated: 2026-05-01
head: session-20260501-1545-trigger-pipeline-shape
merged:
  - session-20260501-2359-cutover
  - session-20260501-1105-engine-wiring
  - session-20260501-1107-zone-topology
  - session-20260501-1237-engine-wiring-stack
  - session-20260501-1132-engine-wiring
  - session-20260501-1528-debt-pass
  - session-20260501-1545-trigger-pipeline-shape
batch:
  - session-20260501-1545-trigger-pipeline-shape
tags: [#checkpoint]
---

# Session Checkpoint

**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- S(trigger-pipeline-shape 2026-05-01 15:46): Discharged ad-20260501-025: reconciled ITriggerPipeline shape — triggers/01 §2 and §7 patched to match effects/02 §3 authoritative async shape with available parameter and TriggerFoldResult.

---

## 🎯 The active open question (singular)

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.
*(From session-20260501-1545-trigger-pipeline-shape.)*

---

## ❓ Other still-open

- id: oq-entity-destruction | **Entity destruction / Cleanup lifecycle** — what "destroyed" means for the Store, when the framework Cleanup hook fires relative to the destruction command envelope, and what happens to in-flight dispatches referencing the destroyed entity. Load-bearing for ongoing effects that self-destruct.
- id: oq-entity-modifier-api | **Entity modifier mutation API (Scaffold bridge)** — concrete payload shapes for `AddModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload` through the `com.scaffold.entities.states` bridge. Needed before any game reads or writes layered modifier values.
- id: oq-attachment-storage | **Attachment storage location** — whether attachments (equipment, auras, buffs) live in a sub-zone on the host, a global attachment registry, or a dedicated zone. Load-bearing for the ongoing-effects attachment model.
- id: oq-stash-release | **Stash release mechanics** — MTG-style scheduling uses `Stash` to hold commands until priority resolves, but the release mechanism (`Release(key)`, ordering rules on release, LIFO ordering) is not designed.
- id: oq-queue-ordering | **Queue ordering shape** — a simple FIFO queue is shown; whether priority queues, interrupt injection, or stack semantics for MTG-like resolution order are built into the engine queue or handled entirely via `GameRules` is unresolved.
- id: oq-gamerules-composition | **`GameRules` composition** — a single `GameRules` implementation is shown; composition contract (provider/policy ordering, enable/disable) is deliberately undesigned for now.
- id: oq-intake-queue | **`IIntakeQueue` shape** — same operations as the original `LinkedList`-typed `Pending`. `CurrentlyExecuting` lives on the queue or on a sibling slice. Pin at engine implementation.
- id: oq-engine-builder | **`GameEngineBuilder` construction protocol** — the builder type used in `pile-counter §9.8` (`WithHandlers`, `WithMutators`, `WithRule`, `WithIntentMapper`, `Build`) and the `services` bag object are undefined anywhere. Load-bearing for any game author.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **1** | **Zone model internal opens** | `oq-zone-internal-opens` — nine sub-questions in `zones/01-zone-model.md`. Design-pass candidate. |
| **2** | **`GameEngineBuilder` construction protocol** | `oq-engine-builder` — load-bearing for any game author. |
| **3** | **Step 8 docs** | Update `PRODUCT-BUILDER.md` with parallel-session model and merge-pass cadence. (REPORT-STYLE.md retired — render templates live inline in each skill.) |

---

## Artifacts to read on resume

- [`features/triggers/models/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md) — ITriggerPipeline shape reconciled (trigger-pipeline-shape)
- [`ledgers/ALIGNMENT-DEBT.md`](ledgers/ALIGNMENT-DEBT.md) — ad-20260501-025 discharged (trigger-pipeline-shape)
- [`features/zones/models/01-zone-model.md`](features/zones/models/01-zone-model.md) — active question source (oq-zone-internal-opens)

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
