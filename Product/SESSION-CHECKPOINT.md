---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Domain-model open-seams design pass landed (2026-05-06, follow-on to the same-day domain modeling pass). 10 decisions locked covering the structural seams that surfaced when drawing the model out: `ISliceProvider` placement on Definition + Instance, `Definition` exposure, sealing convention, `CardRegistry` vs `Catalog`, `CardRef → Card` resolution path, `ZoneVisibilityPolicy` shape, registration-contract formalization, orphan-slice strictness, and setup-command pattern. **Patch cascade pending** — see Active focus.

Earlier today: domain modeling design pass + patch cascade landed (13 decisions). Plan A/B/C state-tech cascade landed earlier in PR #100. `oq-game-definition`, `oq-engine-match-lifecycle`, `oq-gamerules-runtime-definition` all resolved.

---

## 🎯 Active focus

**Patch cascade for the open-seams design pass.** 10 locked decisions to propagate:

- `entities/01-definition-instance-model.md` — `DefinitionWithSlices` parent layering (Q1)
- `entities/02-runtime-surface.md` — `InstanceWithSlices<TDef>` parent + protected `Definition` (Q2, Q3)
- `cards/01-card-model.md` — `Card` unsealed; `CardRegistry` removed; two-hop resolution (Q4, Q5, Q6)
- `zones/01-zone-model.md` — `Zone` unsealed; sealed-record `ZoneVisibilityPolicy` (Q4, Q7)
- `players/01-player-model.md` — `Player` unsealed (Q4)
- `engine/05-game-factory.md` — Registration contract section; drop `CardRegistry` from PileCounter example (Q5, Q8)
- `engine/06-match-lifecycle.md` — Match setup section, host-driven + runtime-driven dispatch (Q10)
- `ledgers/GLOSSARY.md` — retire `CardRegistry`; add `DefinitionWithSlices` / `InstanceWithSlices` / `ZoneVisibilityPolicy`; drop "sealed" wording from Card/Zone/Player
- `ledgers/OPEN-ITEMS.md` — add `bl-zone-visibility-custom-policy-serialization` (Q7 follow-up); optionally `bl-card-resolution-helper-shape` (Q6)

Q9 lock (warning, not error) is status-quo and needs no edit.

After cascade lands: highest-leverage open items remaining are `oq-entity-destruction`, `oq-effect-cancellation-on-match-end`, `oq-attachment-storage`. First vertical-slice prototype (`PileCounterGameDefinition`) becomes the natural next thread.

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
