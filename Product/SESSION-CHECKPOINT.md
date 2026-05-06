---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Plan A/B/C doc cascade **landed** ([Card-Framework PR #100](https://github.com/MgCohen/Card-Framework/pull/100)). Scaffold-side Plan C (`com.scaffold.entities.states` bridge cleanup — `EntityState` pure-data trim, `ClearModifiersPayload` first-class, `StoreEntities.Spawn`, `EntityStateFactory` + `StateEntityOps` deleted) shipped in [Scaffold#45](https://github.com/MgCohen/Scaffold/pull/45). Project-side cascade rewrote every model doc that referenced the retired Scaffold types — GLOSSARY (11 new entries + Retired-terms section), ADR-0005 amendment, entities/01–02, cards/01 + cards/decisions/0001, zones/01 + zones/decisions/0001 + zones/decisions/0002, engine/03–04, effects/01–04, triggers/01, actions/01–02, presentation/01. Plan statuses in `PLANS/Scaffold/` flipped: A/B/C `landed`, D `partially-landed` (Phase 2 docs done; Phase 1 code migration deferred — repo is doc-models-only).

Entities-bridge spike resolved (2026-05-06) — **superseded in favor of keeping the bridge.** Five of the spike's strongest arguments have been mooted by Plan A/B/C: `IReadOnlyEntity`/`IMutableEntity` polymorphism deleted, per-rebuild cache deleted, `IEntityRegistry` replaced by `store.Catalog` + `StoreEntities.Spawn`, dual mutation surface collapsed into `IEntityVariableStorage`, and `EntityDefinition` refactored off `ScriptableObject` to a plain `[Serializable] class`. What remains is two ergonomic concerns (type-safe variable keys, residual mutable-shaped storage surface) — neither justifies a rewrite. `spikes/entities-bridge-replacement.md` flipped to `superseded` with the resolution rationale. Future revisits need fresh evidence (real friction, profiling, authoring concern the bridge can't accommodate).

Prior pass on `oq-game-definition` Q1 locked (suspended after Q1 awaiting full domain modeling pass). Earlier `oq-engine-match-lifecycle` and `oq-gamerules-runtime-definition` cascades fully landed.

---

## 🎯 Active focus

Full domain modeling pass — sketch every slice / state / aggregate the framework needs across cards, zones, players, mappings, building from currently-locked decisions and the post-Plan-C bridge as foundation. Out of scope: commands, handlers, mutators, services, runtime composition (those are already designed). Output: a domain model that becomes the input to Stages 2 (per-kind `ISliceProvider` implementations) and 3 (zone layout, `GameDefinition` shape, `MatchSetup` reshape, asset composition) of the suspended `oq-game-definition` design pass. Starting input: [`spikes/domain-model-proposal.md`](spikes/domain-model-proposal.md).

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

### Kick-start brief — domain modeling session (active thread)

- **Goal.** Iterate on the saved domain model proposal to unblock `oq-game-definition` Stages 2–3.
- **Starting input.** [`spikes/domain-model-proposal.md`](spikes/domain-model-proposal.md) — definition hierarchy, slice inventory by keying axis (framework / game-singleton / zone-keyed / actor-keyed / card-keyed), aggregates survey, factory loop pseudocode, open questions for Stage 2/3. Draft / exploratory; not approved or denied.
- **Foundation.** Post-Plan-C Scaffold bridge — `EntityInstance<TDef>` with typed `Ref<TSubclass> SelfRef`, `EntityState` slice keyed by `Ref<T>`, `IEntityVariableStorage` adapter, seven payloads, `StoreEntities.Spawn`. The entities-bridge spike was resolved superseded; build against the bridge.
- **Out of scope.** Commands, handlers, mutators, services, runtime composition.
- **Why now.** The proposal is the input to per-kind `ISliceProvider` implementations (Stage 2) and `GameDefinition` shape + asset composition (Stage 3). Iterate on the slice surface against the proposal rather than rebuild from scratch.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
