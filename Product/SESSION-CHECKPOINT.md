---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Plan A/B/C doc cascade **landed**. Scaffold-side Plan C (`com.scaffold.entities.states` bridge cleanup — `EntityState` pure-data trim, `ClearModifiersPayload` first-class, `StoreEntities.Spawn`, `EntityStateFactory` + `StateEntityOps` deleted) shipped in [Scaffold#45](https://github.com/MgCohen/Scaffold/pull/45). Project-side cascade rewrote every model doc that referenced the retired Scaffold types: GLOSSARY (added `Reference`/`Ref<T>`/`ICatalog`/`ICatalogged`/`EntityInstance<TDef>`/`EntityState`/`StoreVariableStorage`/`LocalVariableStorage`/`ModifierSource`/`StoreEntities.Spawn`; new "Retired terms" section listing 11 retired types), ADR-0005 amendment (Plan A/B/C cascade header; `Reference`/`Ref<T>` replace `IReference`/`InstanceId`), entities/01 + entities/02 (full bridge-section + handle-surface + lifecycle rewrite), cards/01 + cards/decisions/0001 (Card on `EntityInstance<CardDefinition>` with `Ref<Card> SelfRef`; `CardMapState` is now `CardRef → Ref<Card>` indirection layer; `CardRegistry` engine service for typed handle lookup), zones/01 + zones/decisions/0001 + zones/decisions/0002 (Zone on `EntityInstance<ZoneDefinition>` with `Ref<Zone> SelfRef`), engine/03 + engine/04 (slice-key shapes, ActionTemplate ref types), effects/01–04 + triggers/01 + actions/01 + actions/02 + presentation/01 (incidental sweep — `InstanceId` → `Ref<Card>` / `Reference` everywhere it appears in code samples, payload signatures, ADR-0005 boundary notes). Plan statuses in `PLANS/Scaffold/` flipped: A/B/C `landed`, D `partially-landed` (Phase 2 docs done; Phase 1 code migration deferred — repo is doc-models-only).

Prior pass on `oq-game-definition` Q1 locked (suspended after Q1 awaiting full domain modeling pass). Earlier `oq-engine-match-lifecycle` and `oq-gamerules-runtime-definition` cascades fully landed.

---

## 🎯 Active focus

Resolve the entities-bridge spike (next thread per the user's plan). With Plan A/B/C now landed and the doc cascade complete, the bridge is much cheaper than it was when the spike was drafted (~90 LOC adapter, no caching/multiplexing, identity-free base, slice is pure data). Decide: open `oq-drop-entities-bridge` as a design pass, or punt and proceed against the post-Plan-C bridge into the full domain modeling pass. After that thread resolves, the full domain modeling pass becomes the next active focus.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

### Pending thread — drop the entities bridge? (now upstream-ready)

Sketches in [`spikes/entities-bridge-replacement.md`](spikes/entities-bridge-replacement.md) — **draft / exploratory, not approved or denied**. The cost-side calculus has shifted materially with Plan C landing: bridge is ~90 LOC, identity-free base, pure-data slice, single `ClearModifiersPayload` for atomic transitions. Whether the bridge still earns its keep against a redux-only model is the open question. Next session resumes here per the user's plan: either open `oq-drop-entities-bridge` as a design pass, or punt and proceed with the domain modeling pass against the post-Plan-C bridge.

### Kick-start brief — domain modeling session (queued behind bridge thread)

- **Goal.** Iterate on the saved domain model proposal before resuming `oq-game-definition` Stages 2–3.
- **Starting input.** [`spikes/domain-model-proposal.md`](spikes/domain-model-proposal.md) — definition hierarchy, slice inventory by keying axis (framework / game-singleton / zone-keyed / actor-keyed / card-keyed), aggregates survey, factory loop pseudocode, open questions for Stage 2/3. Draft / exploratory; not approved or denied.
- **Out of scope.** Commands, handlers, mutators, services, runtime composition.
- **Why first (after the bridge thread).** The proposal is the input to per-kind `ISliceProvider` implementations (Stage 2) and `GameDefinition` shape + asset composition (Stage 3). Iterate on the slice surface against the proposal rather than rebuild from scratch.
- **Dependency.** The entities-bridge thread above is upstream — if it lands as "drop the bridge," it reshapes §1 hierarchy and §2.5 (card-keyed) of the proposal. Either resolve it first or take the proposal as-is and resume.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
