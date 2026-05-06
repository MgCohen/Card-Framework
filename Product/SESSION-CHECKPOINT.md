---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Domain modeling design pass + patch cascade **both landed** (2026-05-06). 13 decisions propagated into the canonical Models:

- New `Product/features/engine/models/07-game-definition.md` — `GameDefinition` bundle shape, `ISliceProvider` mechanism, layout-block contract, factory `Build` walk pseudocode, ownership-vs-zone-assignment two-tier model.
- New `Product/features/players/players.md` + `Product/features/players/models/01-player-model.md` — `Player : EntityInstance<PlayerDefinition>, IActor`, `IActor` interface, `PlayerDefinition` shape, per-player slice conventions.
- `Product/features/engine/models/05-game-factory.md` — `MatchSetup = (GameDefinition, Actors, Seed) + 3 overlays`; `IMatchBuilder` updated; validation against new shape; Pile Counter example reshaped to use `PileCounterGameDefinition`; `oq-game-definition` removed from open follow-ups.
- `Product/features/{cards,zones,entities}/models/01-*.md` + `entities/02-runtime-surface.md` — `ISliceProvider` adopted on `CardDefinition`/`ZoneDefinition`/`EntityDefinition`/`EntityInstance<TDef>` with instance-delegates-to-definition default; "slices don't carry their primary key" convention pinned.
- `Product/decisions/0005-objects-over-ids.md` — `IActor` interface added to the polymorphic-types section.
- `Product/ledgers/GLOSSARY.md` — Updated `Actor`/`Player`/`Owner` entries; new entries for `IActor`, `GameDefinition`, `PlayerDefinition`, `ISliceProvider`, `ZoneOverlay`, `PlayerDefinitionOverlay`.
- `Product/ledgers/FEATURE-MAP.md` — `players` row added; engine model count bumped to 7; Mermaid graph extended.
- `Product/ledgers/OPEN-ITEMS.md` — `oq-game-definition` resolved; `bl-actor-ownership-reverse-index` added (deferred Q5 sub-question).
- `Product/spikes/domain-model-proposal.md` — flipped to `superseded` with resolution rationale (locked decisions live in the cascade).

Plan A/B/C doc cascade **landed** earlier in this session ([Card-Framework PR #100](https://github.com/MgCohen/Card-Framework/pull/100)). Entities-bridge spike resolved superseded. Earlier `oq-engine-match-lifecycle` and `oq-gamerules-runtime-definition` cascades fully landed.

---

## 🎯 Active focus

No active thread. Possible next directions:

- **Resolve top open items.** Highest-leverage durable questions left: `oq-entity-destruction` (cleanup lifecycle), `oq-effect-cancellation-on-match-end` (async cancellation), `oq-attachment-storage` (attachment home — sub-zone vs registry vs dedicated zone).
- **Promote a `considered` feature.** `authoring`, `interaction`, or `networking` move from considered to draft when triggered (typically: first feature reaches `implemented`, or a vertical-slice prototype forces it).
- **First vertical-slice prototype.** Pile Counter sample is the canonical reference; building the actual `PileCounterGameDefinition` against the now-settled domain model would force-test the design.

Pick a thread when ready.

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
