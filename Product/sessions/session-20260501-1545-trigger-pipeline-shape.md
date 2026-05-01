---
type: session
schema-version: 2
session-id: session-20260501-1545-trigger-pipeline-shape
parent: session-20260501-1528-debt-pass
status: completed
started-at: 2026-05-01T15:46:03Z
last-updated: 2026-05-01
work-summary: "Discharged ad-20260501-025: reconciled ITriggerPipeline shape — triggers/01 §2 and §7 patched to match effects/02 §3 authoritative async shape with available parameter and TriggerFoldResult."
tags: [#session]
---

# session-20260501-1545-trigger-pipeline-shape

## Part 1 — Active question coming in

**Discharge `ad-20260501-025`** — reconcile `ITriggerPipeline` shape between `effects/02` and `triggers/01`: `effects/02 §3` has `Task<TriggerFoldResult> RunBefore(GameCommand, IReadOnlyList<TriggerMatch>, IEffectScope)`; `triggers/01 §7` has `BeforeFoldResult RunBefore(GameAction, IEffectScope)`. Pick one authoritative shape and patch the other doc.
*(From session-20260501-1528-debt-pass.)*

## Part 2 — Work done

- Patched `triggers/01 §2` ("Execution — ITriggerPipeline"): replaced `BeforeFoldResult`-returning sync interface with the authoritative async `Task<TriggerFoldResult>` shape from `effects/02 §3`; added `available: IReadOnlyList<TriggerMatch>` parameter to both `RunBefore` and `RunAfter`; replaced `BeforeFoldResult` record with `TriggerFoldResult`; updated prose.
- Patched `triggers/01 §7` execute example: method is now `async Task<CommandResult>`; added candidate collection + `FilterTriggers` call; uses `await triggerPipeline.RunBefore(action, available, scope)`; `before.Command` → `before.FinalCommand`.
- Updated `triggers/01 §8` struck-through entry: removed forward reference to ad-20260501-025; replaced with resolution note.
- Struck through `ad-20260501-025` in `ALIGNMENT-DEBT.md`.

## Part 3 — Decisions settled

- **effects/02 §3 is the authoritative `ITriggerPipeline` shape.** Rationale already documented there: async is required so custom pipelines can await a `Decision`; `available` is passed in so the pipeline is decoupled from the registry; `GameCommand` (not `GameAction`) is the base type because folds run on all command types including `CompositeAction` and `GameSignal`; `TriggerFoldResult.FinalCommand` is clearer than `BeforeFoldResult.Command`.

## Part 4 — Debt opened

_(none)_

## Active question going out

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.

## Part 5 — Questions opened / closed

- Closed: `ad-20260501-025` — ITriggerPipeline shape conflict discharged.
