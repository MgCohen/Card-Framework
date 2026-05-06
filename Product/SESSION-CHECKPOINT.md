---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Open-seams design pass + patch cascade **both landed** (2026-05-06, follow-on to the same-day domain modeling pass). 10 decisions propagated:

- `entities/01-definition-instance-model.md` — §"Definitions" reframed around the `DefinitionWithSlices` parent class.
- `entities/02-runtime-surface.md` — §1 reshaped: `InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider` parent class, `Definition` is `protected`, subclass-re-export discipline documented.
- `cards/01-card-model.md` — `Card` unsealed; `CardRegistry` removed; new "Resolution surfaces" section pinning the two-hop path; mutation expansions and Read API updated.
- `zones/01-zone-model.md` — `Zone` unsealed; visibility policy replaced with the sealed-record `ZoneVisibilityPolicy` hierarchy; Read API split between `GetCardRefs(state)` and `GetCards(store)`.
- `players/01-player-model.md` — `Player` unsealed; `PlayerDefinition` re-parented onto `DefinitionWithSlices`.
- `engine/05-game-factory.md` — added "Registration contract" section listing framework-auto-registered, game-required, game-optional registrations.
- `engine/06-match-lifecycle.md` — added §4.4 "Match setup" covering host-driven and runtime-driven setup-command dispatch.
- `engine/07-game-definition.md` — `ISliceProvider` reach-points reframed around the parent classes; `EntityInstance<TDef>` examples updated.
- `ledgers/GLOSSARY.md` — `CardRegistry` retired (moved to Retired terms); new entries for `DefinitionWithSlices`, `InstanceWithSlices<TDef>`, `ZoneVisibilityPolicy`; `Card`/`Zone`/`Player` entries updated to drop "sealed" and reflect parent classes.
- `ledgers/OPEN-ITEMS.md` — added `bl-zone-visibility-custom-policy-serialization` (Q7 follow-up) and `bl-card-resolution-helper-shape` (Q6 follow-up).

Earlier today: domain modeling design pass + patch cascade landed (13 decisions). Plan A/B/C state-tech cascade landed earlier in PR #100. `oq-game-definition`, `oq-engine-match-lifecycle`, `oq-gamerules-runtime-definition` all resolved.

---

## 🎯 Active focus

No active thread. Highest-leverage open items remaining: `oq-entity-destruction` (cleanup lifecycle), `oq-effect-cancellation-on-match-end` (async cancellation), `oq-attachment-storage` (attachment home). First vertical-slice prototype (`PileCounterGameDefinition` against the now-settled domain model) is the natural next thread — the open-seams pass cleaned up the seams that would have bitten during construction.

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
