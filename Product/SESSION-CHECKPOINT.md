---
type: checkpoint
last-updated: 2026-05-01
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-05-01 (session 17)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

**Comprehensive doc audit + workflow patch (S17).** Audit across 13 Models / 7 feature roots / 3 ADRs surfaced 41 findings; 26 genuinely new (8 already tracked, 7 partial). Triaged into ledgers: **7 alignment-debt entries** across 5 cascade groups (forces pass-mode per §5.8), 11 new open-questions, 2 backlog (interaction + authoring feature folders). Root-caused the drift to a cascade-walk gap and patched `PRODUCT-BUILDER.md` §3/§4.3/§5.4/§5.7/§5.9: §5.7 now has 3 mandatory substeps (1a `builds-on:` / 1b retired-token grep / 1c shadow-check), §4.3 has a `superseded` status convention parallel to `abandoned`, §5.4 extends to ADR-or-Model.

---

## The active open question (singular)

**Pass-mode discharge of 7 alignment-debt entries.** Forced by §5.8 threshold. All 7 are mechanical cascades from already-settled decisions (engine/02 contract, effects/02 FoldResult, S15 awaiter, S14 Scaffold types, S15-16 Scope deletion). Run `/builder:debt-pass` or shape S18 as an alignment pass per §5.5. After clearance, return to `GameState` slice catalog.

---

## Other still-open

Carried forward from `ledgers/OPEN-QUESTIONS.md`. Items the next few sessions are likely to touch.

- **`GameState` slice catalog** — load-bearing for engine wiring. Original S17 active question, re-active after pass clears.
- **Engine wiring stack (NEW S17)** — `IHandlerRegistry` interface, `GameEngineBuilder` construction protocol, `ICommandPipeline` interface, engine startup / first `OnQueueDrained` trigger, `GameRules` service wiring. Cluster — could be one design-pass session.
- **`ITriggerSystem` vs `ITriggerPipeline` unification (NEW S17)** — two competing shapes for the trigger fold execution + subscription surface (`triggers/01` vs `effects/02`). Decision needed.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **Game lifecycle design / `EndGameCommand` semantics** — session start, turn structure, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing for regular cards (not just attachments). Depends on game lifecycle.
- **Zone model internal opens (NEW S17)** — 9 questions in `zones/01-zone-model.md` "Open Questions Left For Later"; membership storage / visibility / actor semantics blocking.
- **Entity destruction / Cleanup lifecycle (NEW S17)** — what "destroyed" means for the Store; ongoing-effects depend on it.
- **`HistoryEnvelope` typed definition (NEW S17)** — currently pseudocode; load-bearing for replay + multiplayer.
- **Intent → template lookup (NEW S17)** — gateway side of `IIntentMapper.Map(template, choice)`.
- **FSM-shaped game state (reframed S16).** Awaiter succession. Lower urgency.
- **Multi-actor / multi-open awaits.** Pin when an async-decision-heavy game arrives.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **1** | **Pass-mode discharge** | Forced by §5.8. 7 mechanical entries. `/builder:debt-pass` or manual S18 as alignment pass. |
| **2** | **`GameState` slice catalog** | Re-active after pass clears. Concrete slice list + ownership model. Likely a dedicated Model under `features/engine/` or `features/state/`. |
| **3** | **Engine wiring design pass** | `IHandlerRegistry` + `GameEngineBuilder` + `ICommandPipeline` + `ITriggerSystem`/`ITriggerPipeline` unification + startup. Cluster of 5 new opens — one design-pass session. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1. Cheap leverage on every session. |
| **WF-2** | **`/builder:design-pass` skill** | Q-by-Q with options + lean + verdict. S15 is the canonical example. |
| **12** | **Game lifecycle design** | Best unblocked task after slice catalog. New feature folder (`features/lifecycle/`?) or in `engine/`. |
| **7** | **`PLANS/` — first implementation slice** | Pull after slice catalog and lifecycle land and at least one feature is ready for build-gate consideration. |
| **15** | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. **Updated S17:** §3, §4.3, §5.4, §5.7 (1a/1b/1c substeps), §5.9. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | **7 open entries (S17 audit).** Pass-mode source for next session. |
| [sessions/session-17.md](sessions/session-17.md) | S17 audit + ledger triage + workflow patch. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | Canonical effect-flow contract. §6 patched in S16; `ICommandAwaiter` settled. |
| [sessions/session-16.md](sessions/session-16.md) | S16 execution record. §6 patch + cascade, pile-counter re-lock. |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. **+11 new still-open after S17.** |
| [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) | Layer hierarchy, two dispatch paths. |
| [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) | Snapshot production; cascade completed in S16 (§4 Scope drop, §7 handler example). |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample. **Status: `locked`** re-locked S16. (S17 debt entry on §9.1 stale `Scope` mention — minor.) |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
