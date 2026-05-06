---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Domain modeling design pass landed (2026-05-06) — **13 questions settled**, no doc edits yet. Locked: bridge coverage (Card/Zone/Player all on `EntityInstance<TDef>`); `Actor` as `IActor` interface; `ISliceProvider` parameterless on both Definition and Instance (instance delegates to definition); slices externally keyed; two-tier ownership (declarative zone layout + runtime `OwnershipState`); `GameDefinition` standard bundle (`Rules + SharedZones + CardCatalog + DefaultPlayerDefinition + GetInitialSlices()`); `MatchSetup = (GameDefinition, Actors, Seed) + three overlays`; `CardDefinition` adopts `ISliceProvider` from day one; no framework aggregates this pass; `IDefinition` marker deferred. Patch cascade is the next thread: ~10 docs touched, including a new `engine/07-game-definition.md` and a new `players/` feature.

Plan A/B/C doc cascade **landed** ([Card-Framework PR #100](https://github.com/MgCohen/Card-Framework/pull/100)). Scaffold-side Plan C (`com.scaffold.entities.states` bridge cleanup) shipped in [Scaffold#45](https://github.com/MgCohen/Scaffold/pull/45). Project-side cascade rewrote every model doc that referenced the retired Scaffold types. Plan statuses in `PLANS/Scaffold/` flipped: A/B/C `landed`, D `partially-landed` (Phase 2 docs done; Phase 1 code migration deferred — repo is doc-models-only).

Entities-bridge spike resolved (2026-05-06) — **superseded** in favor of keeping the bridge (five of the spike's strongest arguments mooted by Plan A/B/C; remaining concerns don't justify a rewrite).

Earlier `oq-engine-match-lifecycle` and `oq-gamerules-runtime-definition` cascades fully landed.

---

## 🎯 Active focus

Patch cascade for the domain modeling design pass — propagate the 13 locked decisions into the canonical Models. Touch list:

- `engine/05-game-factory.md` — `MatchSetup` reshape (Q10), `GameDefinition` parameter (Q9), `Build` ordering corollary (Q7).
- New `engine/07-game-definition.md` — `GameDefinition` bundle shape, `ISliceProvider` mechanism, layout-block contract, `Build` walk pseudocode.
- `zones/01-zone-model.md` — `ZoneDefinition : ISliceProvider`; declarative layout block on `GameDefinition.SharedZones` / `PlayerDefinition.PerPlayerZones`.
- New `players/` feature — `Player : EntityInstance<PlayerDefinition>, IActor`, `PlayerDefinition` shape, per-player slice convention (Q6).
- `cards/01-card-model.md` — `CardDefinition : ISliceProvider` with default empty implementation; instance-delegates-to-definition spawn pathway.
- `entities/01` + `entities/02` — `ISliceProvider` on both Definition and Instance; "slices don't carry their primary key" convention pinned.
- `GLOSSARY.md` — Add `IActor`, `ISliceProvider`, `GameDefinition`, `PlayerDefinition`, `MatchSetup` overlays; update `Actor` (interface) and `Player` (on bridge + IActor) entries.
- `OPEN-ITEMS.md` — close `oq-game-definition`; add `bl-actor-ownership-reverse-index` (deferred Q5 sub-question).
- `FEATURE-MAP.md` — add `players` feature row.
- `spikes/domain-model-proposal.md` — flip to `superseded`; locked decisions live in the eventual Models.

Out of scope (still): commands, handlers, mutators, services, runtime composition (already designed).

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

### Locked decisions — domain modeling design pass (2026-05-06)

13 questions settled. Decisions live here until the patch cascade lands them in the canonical Models.

- **Q1 = B** — Card/Zone/Player all extend `EntityInstance<TDef>`; their definitions all extend `Scaffold.EntityDefinition`.
- **Q2 = B** — `PlayerDefinition` adopts `ISliceProvider` from day one.
- **Q3 = A** — `Actor` is an interface (`IActor` carrying `ActorId`); `Player : EntityInstance<PlayerDefinition>, IActor`. Non-entity actors are lightweight records implementing `IActor`. Actor's purpose stays narrow — subject to revisit.
- **Q4 = D** — `ISliceProvider` is parameterless: `IEnumerable<BaseState> GetInitialSlices()`. Implemented on both Definition and Instance; instance delegates to definition by default. **Slices don't carry their primary key**; storage is keyed externally by the factory at the instance's `SelfRef`.
- **Q5 = A** — Two-tier ownership: zone assignment is declarative (layout blocks); `OwnershipState` shrinks to runtime mutable card ownership. Reverse-index deferred.
- **Q6 = C** — Per-player slice shape convention: per-player-keyed for substantial state; singleton-with-dict for cross-cutting indexes.
- **Q7 = A** — Actor-aware singletons (`TurnState.Active`) start with default/null; setup commands populate after `StartMatch`. Build orders per-spawn instances first, game singletons second.
- **Q8 = B** — Zone layout: declarative blocks on `GameDefinition.SharedZones` / `PlayerDefinition.PerPlayerZones` + `MatchSetup.ZoneOverlay` escape hatch.
- **Q9 = B** — `GameDefinition` shape: `Rules + SharedZones + CardCatalog + DefaultPlayerDefinition + GetInitialSlices()`.
- **Q10 = A** — `MatchSetup = (GameDefinition, Actors, Seed) + 3 overlays` (`InitialSlices`, `ZoneOverlay`, `PlayerDefinitionOverlay`). `Rules` moves out of `MatchSetup`.
- **Q11 = A** — `CardDefinition` adopts `ISliceProvider` from day one with default empty implementation.
- **Q12 = B** — Defer `IDefinition` marker.
- **Q13 = A** — No framework aggregates introduced this pass.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
