---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

The previous checkpoint (to 2026-05-31, step 5b) is in git history: `git show 14a796c:Product/SESSION-CHECKPOINT.md` (session snapshots under `sessions/` are gitignored).

---

## Where we stopped

**Roadmap phase 0 — housekeeping (2026-10-18).** The plan of record is [`spikes/framework-roadmap.md`](Product/spikes/framework-roadmap.md). The current code is described in [`docs/architecture.md`](docs/architecture.md).

Done in phase 0:
- Merged #184 (architecture doc + roadmap), #182 (test tiers under `Framework/Testing/`, `docs/testing.md`) and #183 (Unity 6000.6.6f1). EditMode on merged `main`, Unity 6000.6.6f1: **364/364 pass**. PlayMode was not run after the NGO 2.4 → 2.13 upgrade.
- Closed stale PRs #18, #19, #101, #148, #156, #159 (branches kept).
- Wrote [ADR-0014](Product/decisions/0014-service-first-state-model.md) (service-first state model). ADR-0009 is superseded; 0005, 0007, 0010–0013 are superseded in part.
- Archived all 37 old-model docs under `Product/features/**` into their feature `archive/` folders. Built parts are `superseded` (by `docs/architecture.md`, ADR-0014 or a spike); unbuilt parts are `abandoned` and read only for their open questions.
- Replaced the glossary with one that names only types on `main`. The old glossary is in [`_archive/ledgers/GLOSSARY-old-model.md`](Product/_archive/ledgers/GLOSSARY-old-model.md). Old-model terms and `*Layer` names are in `_workflow/retired-tokens.json`.
- Renamed `*Layer` → `*Installer` in the active spikes; updated `state-service.md` to the post-#165 slot-based `StatePushEvent`; archived the two step-4 build logs.
- Swept [`ledgers/OPEN-ITEMS.md`](Product/ledgers/OPEN-ITEMS.md) against the new glossary and ADR-0014: 10 entries closed (answered by code or ADR-0014, or owned by a roadmap phase), the rest rewritten in current vocabulary with an owning roadmap phase. The roadmap's phase inputs now list the OPEN-ITEMS ids they resolve.
- Archived `general/ARCHITECTURE.md` (research-phase, old model) to `general/archive/`; `docs/architecture.md` replaces it.

**What landed between 2026-05-31 and phase 0** (all on `main`):
- 05-31 – 06-01: step 5b `Framework.States`; conformance PRs #161–#166 (Core `TypeKey` / `SlotRegistry`, `EntityType` records, typed tag store, slot-based `StatePushEvent` + `GameStateRegistry`, read-only store views with `internal` writers).
- 06-02: cleanup PRs #167–#175 (wire dedup, `BootstrapLayer` base, one type per file, channel-id guard, `PeerRoster` split from `Session`, player DTO properties, store-miss diagnostics).
- 06-03 – 06-05: integration spine (#177); step 5c `Framework.Commands` (#178: `CommandService.Execute`, `CommandHandler<T>`, `CommandValidator<T>`); acceptance suite phases 1–2 (#179, #181); flat installers under one `GameplayLayer` (#180).

---

## 🎯 Active focus

**Finish phase 0, then review the roadmap order before phase 1 (engine loop).**

Left in phase 0:
- Retired tokens still appear in some active docs outside phase 0's scope: ADR bodies (append-only; banners point at ADR-0014), the glossary's *Retired terms* table, and old-model spikes and samples (`match-state-redesign.md`, `runtime-walkthrough.md`, `support/samples/pile-counter.md`, `FEATURE-MAP.md`, `_drafts/template-pilot/`). Tracked by OPEN-ITEMS `bl-spike-status-migration`; `/builder:lint` T1 needs an allow-list for ADRs and the retired-terms table.
- Optional: CI that runs EditMode tests on each PR (needs a Unity license secret), `.editorconfig`, warnings as errors.
- Run PlayMode tests (`Framework.Wire.PlayTests`, `Framework.Multiplayer.PlayTests`) on 6000.6.6f1.

Next: phase 1 — replace `CommandService.Execute` with `Enqueue` + pump, async commands that await requests, nested blocks. Inputs: `spikes/command-service.md` § Deferred items.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto, not yet promoted to OPEN-ITEMS.*

- **Polymorphic wire events for subclass-based `PlayerData` / `PlayerContext` extension.** Deferred; revisit when players become entities (roadmap phase 3).
- **Post-match phase.** `SessionState` is `Connecting | InLobby | InMatch | Disconnected`. What happens after `MatchEndedState` (rematch, back to lobby)? Revisit in roadmap phase 2 when Multiplayer connects to gameplay.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
