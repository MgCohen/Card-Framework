---
type: session
schema-version: 1
status: completed
date: 2026-05-01
tags: [#session]
---

# Session 16 — 2026-05-01

## Part 1 — Content (cap ~50 lines)

### 🎯 Active question coming in
**§6 patch + cascade.** Design fully settled in S15; this is the mechanical execution session. Open one alignment-debt entry per cascade target and discharge in the same session per §5.5. Targets: `04-effect-flow §4.5 / §5 / §6 / §10 / §11`, `03-snapshot-pipeline §4 / §7 / §9`, `01-command-pipeline §3`, `02-availability-snapshot`, `01-presentation-loop`, `pile-counter` mapper constructor, `OPEN-QUESTIONS` snapshot-scope-as-FSM reframe.

### Work done
- 12 alignment-debt entries opened and discharged in same pass (§5.5)
- `pile-counter.md` re-locked: all four §4.4 lockbar criteria met (settled deps, no debt, ≥1 stable session)
- `04-effect-flow.md` §4.4/§4.5/§5/§6/§9/§10/§11 patched: `IDecisionResolver` → `ICommandAwaiter`, §5 rewritten as type-only routing, §6 rewritten with engine-level awaiter check + decision-unaware gateway/mapper, invariants updated
- `03-snapshot-pipeline.md` §4/§6/§7/§9 patched: `SnapshotScope`/`FreeInput`/`DecisionPrompt` deleted; snapshot is templates-only; handler example updated; FSM open item reframed
- `01-command-pipeline.md` §3 gateway note added confirming decision-unaware boundary
- `02-availability-snapshot.md` — `FreeInput` removed from snapshot calls
- `01-presentation-loop.md` — Scope prose removed; awaiter pre-check added to §10 step 8; §11/§12/§13 updated
- `pile-counter.md` — mapper constructor comment updated; `FreeInput` removed from `Actions.Set`; mapper in `03-snapshot-pipeline §3` fixed (was missing `IStateScope` injection)
- `OPEN-QUESTIONS.md` — snapshot-scope-as-FSM entry reframed as FSM-shaped game state (awaiter succession)
- `ALIGNMENT-DEBT.md` — 12 entries discharged, ledger clean

### Decisions settled
- `ICommandAwaiter` is the canonical service name replacing `IDecisionResolver`
- No decision id anywhere — type-only routing (`cmd is TCommand`) is the sole key
- `SnapshotScope` / `FreeInput` / `DecisionPrompt` are deleted; snapshot is `AvailableActionsSnapshot { Templates }`
- Engine-level pre-pipeline awaiter check short-circuits matched commands (no envelope, handler, or triggers)
- Mapper and gateway are fully decision-unaware; ADR-0006 restored

### 🎯 Active question going out
`GameState` slice catalog — concrete catalog of slices (`PilesState`, `LastMoveState`, `ZonesState`, `TurnState`, `AvailableActionsState`) on top of Scaffold `Store`. Next unblocked candidate after §6 patch landed.

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created
_(none)_

### Ephemeral questions (scratch, not promoted)
- `AvailableActionsSnapshot` in `01-presentation-loop §4` still shows the old single-record `GameState` with `Piles` / `LastMove` fields — this is a pre-existing stale sketch from before the slice model, not introduced by this session.

### References
- Docs touched: `04-effect-flow.md`, `03-snapshot-pipeline.md`, `01-command-pipeline.md`, `02-availability-snapshot.md`, `01-presentation-loop.md`, `pile-counter.md`, `OPEN-QUESTIONS.md`, `ALIGNMENT-DEBT.md`
- Related sessions: 14 (prior cascade), 15 (design pass), 16 (this session)
