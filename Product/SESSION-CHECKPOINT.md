---
type: checkpoint
last-updated: 2026-05-01
tags: [#checkpoint]
---

# Session Checkpoint

**Last updated:** 2026-05-01 (session 15)
**Replaceable.** History lives in `sessions/`. Never append here.

---

## Where we stopped

- **§6 boundary redesigned (design pass; no doc edits yet).** Eight design questions walked Q-by-Q. Architectural reframe: input layer is decision-unaware. Mapping is state-driven for normal reasons (game phase, narrowing); decisions are just one source of `AvailableActions` narrowing. `IIntentMapper` scope branching is gone for good.
- **`DecisionResponseCommand` / `DecisionResponseHandler` / `DecisionPrompt(CommandId)` snapshot scope deleted from the model.** Decision answers are real game commands (`SelectTargetCommand`, `PickOptionCommand`, …) routed by command type alone.
- **`IDecisionResolver` reshaped to `ICommandAwaiter`.** Final shape: `Task<TCommand> Wait<TCommand>(CancellationToken ct = default)` + `bool TryResolve(GameCommand cmd)`. No `AwaitingFor` id, no `IAwaitResponse` marker. Routing is closure-captured `cmd is T` pattern matching. Single-open per type-hierarchy is a runtime assert.
- **Engine-level pre-pipeline awaiter check.** Matched commands short-circuit (no envelope, no handler, no triggers); non-matches fall through to the normal pipeline. Cancel-as-command for player path; `CancellationToken` for engine-internal unwinds.
- **Mapper + gateway both decision-unaware.** `IIntentMapper.Map(template, choice) → GameCommand` switches on template subtype only (ADR-0006 restored). Gateway flow: intent → mapper → `engine.Dispatch` → awaiter check. No scope-reading state injection on the mapper. Snapshot templates carry no awaiter-routing fields — type-only routing means no `AwaitingFor` plumbing.
- **ADR-0006 + ADR-0007 unchanged.** This restores the §6 contract to ADR-0006's chosen shape rather than mutating it.

---

## The active open question (singular)

**§6 patch + cascade.** Design fully settled in S15; this is the mechanical execution session. Open one alignment-debt entry per cascade target and discharge in the same session per §5.5. Targets: `04-effect-flow §4.5 / §5 / §6 / §10 / §11`, `03-snapshot-pipeline §4 / §7 / §9`, `01-command-pipeline §3`, `02-availability-snapshot`, `01-presentation-loop`, `pile-counter` mapper constructor, `OPEN-QUESTIONS` snapshot-scope-as-FSM reframe.

Mostly mechanical edits + a clean cascade. Sample re-lock and the slice-catalog work are the next two candidates after this lands.

---

## Other still-open

Carried forward from `ledgers/OPEN-QUESTIONS.md`. Items the next few sessions are likely to touch.

- **`GameState` slice catalog** — concrete catalog of slices (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`, decisions slice, etc.) on top of Scaffold `Store`. Load-bearing for engine wiring.
- **`IIntakeQueue` shape** — pin at engine implementation.
- **Game lifecycle design** — session start, turn structure, phase transitions, end conditions. Prerequisite for trigger subscription lifecycle.
- **Trigger subscription lifecycle** — Register/Unregister timing relative to zone transitions. Depends on game lifecycle.
- **`EndGameCommand` semantics** — bleeds into session lifecycle.
- **Snapshot scope as state machine (reframed).** Now sits *behind* the awaiter rather than in front of it — the FSM-shaped state model is the eventual successor to the type-only routing the awaiter does today. Lower urgency.
- **Multi-actor / multi-open awaits.** Today single-open is a runtime assert. Type-only routing is structurally tractable for distinct types; pin when an async-decision-heavy game arrives.

---

## Suggested next steps

| # | Task | Notes |
|---|------|-------|
| **20-§6-patch** | **§6 patch + cascade** | Active question. Mechanical execution of the S15 design pass. ~7 cascade sites. Open + discharge alignment-debt in a single pass per §5.5. |
| **20-relock** | **Re-lock pile-counter** | Status `draft` after session 14's §9 rewrite. Re-lock per §4.4 lockbar after one stable session with no further contract edits. |
| **2** | **`GameState` slice catalog** | Now load-bearing in `OPEN-QUESTIONS.md`. Concrete slice list + ownership model. Likely needs a dedicated Model under `features/engine/` or `features/state/`. |
| **WF-1** | **Tier 1 slash commands** | Per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8 Tier 1. Cheap leverage on every session. |
| **WF-2** | **`/builder:design-pass` skill** | Q-by-Q with options + lean + verdict. S15 is the canonical example. |
| **12** | **Game lifecycle design** | Best unblocked task after §6 + slice catalog. New feature folder (`features/lifecycle/`?) or in `engine/`. |
| **7** | **`PLANS/` — first implementation slice** | Pull after §6, slice catalog, and lifecycle land and at least one feature is ready for build-gate consideration. |
| **15** | **MTG-strict SBA ordering** | Defer until a game needs it. Captured as `features/engine/decisions/0006-mtg-sba-known-difference.md`. |

---

## Artifacts to read on resume

| File | Why |
|------|-----|
| [_workflow/PRODUCT-BUILDER.md](_workflow/PRODUCT-BUILDER.md) | The workflow itself. |
| [ledgers/FEATURE-MAP.md](ledgers/FEATURE-MAP.md) | Bird's-eye view of features, Models, ADRs. |
| [ledgers/ALIGNMENT-DEBT.md](ledgers/ALIGNMENT-DEBT.md) | 0 open entries. Patch session opens the next batch. |
| [features/effects/models/04-effect-flow.md](features/effects/models/04-effect-flow.md) | Canonical effect-flow contract. **§6 is the patch target;** §4.5 (`IDecisionResolver` → `ICommandAwaiter`), §5 (decision-id-≡-command-id collapse, simplifies under type-only routing), §10 invariants. |
| [sessions/session-15.md](sessions/session-15.md) | Design-pass record. Q1–Q8 verdicts + final `ICommandAwaiter` shape. **Read before patching.** |
| [ledgers/OPEN-QUESTIONS.md](ledgers/OPEN-QUESTIONS.md) | Durable design questions ledger. |
| [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md) | Layer hierarchy, two dispatch paths. |
| [features/engine/models/03-snapshot-pipeline.md](features/engine/models/03-snapshot-pipeline.md) | Snapshot production; cascade target (§4 Scope drop, §7 handler example update). |
| [features/engine/models/02-gamerules-contract.md](features/engine/models/02-gamerules-contract.md) | `GameRules` interface — services injected, four methods. |
| [support/samples/pile-counter.md](support/samples/pile-counter.md) | Floor sample. **Status: `draft`** after session 14 rewrite. Re-lock candidate. |

For the Tier 1 commands (WF-1):

| File | Why |
|------|-----|
| [../ProductBuilder/AI-FIRST-AUTOMATION.md](../ProductBuilder/AI-FIRST-AUTOMATION.md) | Catalog of slash commands and skills, with §3 detailed specs and §8 sequencing. |

---

## How to use this file

- **On milestone / session end:** Edit in place. Refresh date, "where we stopped," and "next steps."
- **Keep it short** — if it grows past two screens, trim. Detail belongs in `features/<feature>/` and `sessions/`.
- **Optional commit/PR line:** "Checkpoint: `Product/SESSION-CHECKPOINT.md`."
