---
type: session
schema-version: 2
session-id: session-20260502-1446-objects-over-ids-audit
parent: session-20260501-1545-trigger-pipeline-shape
status: completed
started-at: 2026-05-02T14:46:00Z
date: 2026-05-02
work-summary: "Objects-over-IDs audit: 8 cascade entries discharged — all ADR-0005 boundary uses documented across effects/02, effects/03, effects/04, engine/01, engine/03, actions/01, actions/02, presentation/01."
tags: [#session, #debt-pass]
---

# session-20260502-1446-objects-over-ids-audit

## Part 1 — Active question coming in

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.
*(From session-20260501-1545-trigger-pipeline-shape.)*

## Part 2 — Work done

- ad-040 (effects/02): `InstanceId owner` in `OnBefore<T>` and `TriggerMatch.Owner` are stable subscription keys — ADR-0005 §Stable subscription keys explicitly exempts these. Documented with boundary notes in §1 and §3.
- ad-041 (effects/03): `InstanceId self` in `AbilityDefinition.Register` is a stable subscription key. Documented with boundary note in §2.
- ad-042 (engine/01): `OnTimeExpired(ActorId actor)` is a type-mismatch boundary (timer has no Actor object); `GetActionsFor(ActorId actor)` is on the presentation-consumer side. Both boundary-correct. Documented with notes in §4 and §10.
- ad-043 (actions/01): Command records (`GameAction`, `PlayCardCommand`) are wire-form dispatch/serialization/transport — all ID fields boundary-correct. Documented with ADR-0005 boundary note on GameCommand/GameAction base.
- ad-044 (effects/04): Mutator payloads are Store wire-form — boundary-correct for IDs. Handler code building payloads should derive IDs from held objects. Documented with boundary note in §4.2.
- ad-045 (engine/03): ActionTemplate records are snapshot wire-form — IDs boundary-correct. Rule-side construction derives IDs from held objects. Documented with ADR-0005 boundary note in §3.
- ad-046 (actions/02): Same as ad-045. Documented with ADR-0005 boundary note in §2.
- ad-047 (presentation/01): Presentation is the wire-form consumer side — IDs boundary-correct in AffordanceMap. Documented with ADR-0005 boundary note in §6.
- debt: none opened

## Part 3 — Decisions settled

- All 8 Objects-over-IDs cascade entries are **boundary-correct as-is**. No API changes needed. The debt entries were opened under the assumption that subscription-key and wire-form uses were non-boundary violations; ADR-0005's own exceptions list (stable subscription keys, serialization, transport, type-mismatch) covers all of them.

## Part 4 — Active question going out

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.
*(From session-20260501-1545-trigger-pipeline-shape.)*
