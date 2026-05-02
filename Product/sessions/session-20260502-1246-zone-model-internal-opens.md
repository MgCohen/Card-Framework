---
type: session
schema-version: 2
session-id: session-20260502-1246-zone-model-internal-opens
parent: session-20260501-1545-trigger-pipeline-shape
status: completed
started-at: 2026-05-02T12:46:52Z
last-updated: 2026-05-02
date: 2026-05-02
work-summary: "Auto debt-pass: 2 cascade + 0 linter discharged, 0 stragglers caught, 8 skipped."
tags: [#session, #debt-pass]
---

# session-20260502-1246-zone-model-internal-opens

## Part 1 — Active question coming in

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.

## Part 2 — Work done

- 2 cascade entries discharged (ad-20260502-001, ad-20260502-002): added `IEntityRegistry Entities` and `IModifierFactory Modifiers` to `IEffectScope` sketches in effects/01 §7 and actions/01 §7
- 0 linter entries discharged
- 0 stragglers caught
- 8 skipped — needs a session: ad-040–ad-047 (Objects-over-IDs cascade — all require boundary/non-boundary judgment; several may be boundary-correct per ADR-0005 "stable subscription keys" exception)
- retired-tokens.json: no new terms (additive edits only)
- debt: none opened
- backlog: bl-debt-pass-gha-silent-failure added (GHA debt-pass produced no output on 2026-05-02 organic fire; root cause unknown)

## Part 3 — Decisions settled

- none (debt-pass is mechanical)

## Part 4 — Active question going out

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.
*(From session-20260501-1545-trigger-pipeline-shape.)*
