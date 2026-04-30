---
type: session
status: completed
last-updated: 2026-04-30
date: 2026-04-30
tags: [#session]
---

# Session 13 — 2026-04-30

## Part 1 — Content (cap ~50 lines)

### Active question coming in
**Effect-flow doc.** Effects mid-execution need `IAvailableActions` and `IGameStateProvider` (likely through `IHandlerContext`). The full effect lifecycle (async orchestration, decision-id threading, await semantics, suspension/resumption) is scattered across `features/effects/models/01-async-dispatch.md`, `features/engine/models/03-snapshot-pipeline.md` §7, and the implicit handler-context placeholders in `support/samples/pile-counter.md`. Needs a single canonical Model under `features/effects/models/`.

### Work done
- Wrote `features/effects/models/04-effect-flow.md` (status: `settled`, builds-on `effects/01`, `engine/03`).
- Walked the §8 open-questions list from `effects/01` and answered Q1–Q5; settled `Effect` as an opaque first-class abstract type, per-instance, definition holds the recipe (out of scope here).
- First-pass shapes pinned: `IEffectScope` (`State` + `Dispatch` + `Cancel`), `IHandlerContext` (`Store` + `Actions` + `Resolver`), `IDecisionResolver` (TCS keyed by `CommandId`), `DecisionResponseCommand` + handler at the uniform boundary, `ICommandHandler.Handle` returns `Task<TResult>`.
- Doc review pass — user feedback drove second-round patches:
  - **§4.1/§4.2 rewritten against real Scaffold State types.** Replaced invented `IGameStateProvider` / `IStore` / `IStateMutator<TPayload>` with `IStateScope` (read), concrete `Store` (read + execute), and `Mutator<TState, TPayload>` with `Change(state, payload, scope)`. Pinned the one-payload-many-registered-mutators fan-out as the canonical write path.
  - **§4.4 typed `Store` as concrete Scaffold class** and added a follow-up flag on context-vs-injected-services for future review.
  - **§4.5 flagged for full review** — TCS-keyed-by-`CommandId` is good enough for now and not load-bearing; subsumed by the FSM-shaped successor when that lands.
  - **§8.1 trimmed Fireball** — dropped the `MoveToDiscardAction` line (that's spell-cast/resolution flow, not the effect's job).
  - **§8.2 collapsed to a single `Store.Execute(payload)` call**; `PileMoveMutator` and `LastMoveMutator` register against `MoveCardPayload` at bootstrap and both fan out from one dispatch.
  - Updated §10 invariants to match the Scaffold-bound shapes.
- **§6 (uniform boundary) deferred.** User flagged the IntentMapper-decides-shape pattern as suspect; redesign discussion is the next active question.
- Decision id ≡ `CommandId` of the action that opened the decision (settled). `DecisionPrompt(CommandId Id)` replaces the `DecisionId` parallel registry in `03-snapshot-pipeline.md` §4.
- Mutation discipline locked: effects read but never mutate; handlers read + invoke mutators (via `Store.Execute(payload)`); mutators are the only producers of new state.
- Cancellation: action-cancellation surfaces in `CommandResult`; effect-self-abort via `scope.Cancel()`. Effect always decides whether to continue.
- Opened **12 alignment-debt entries** in `ledgers/ALIGNMENT-DEBT.md`: 6 from the original Model edits (sample §9.4/§9.5/Q8, snapshot-pipeline §4/§7, command-pipeline `Handle` signature) plus a new "Cascade — Scaffold State/Store types audit" subsection covering 6 more docs that referenced invented types (engine/02, engine/03, both engine ADRs, sample-wide, OPEN-QUESTIONS).
- Resolved `OPEN-QUESTIONS.md` — "Effect-side service symmetry" struck through with link to the new Model.
- Updated `FEATURE-MAP.md` — `effects` row now 4 models; model-level breakdown lists `04-effect-flow`.

### Decisions settled
- Effect-flow canonical contract (services bound to Scaffold State types, decision threading, mutation discipline) — `features/effects/models/04-effect-flow.md`.

debt: 12 opened

### Active question going out
**Alignment-debt discharge (forced by §5.8 threshold).** 12 open entries vs. threshold of 5 — next session is workflow-mandated pass-mode. Two batches: 6 from the new Model's Direct edits (pile-counter §9.4/§9.5/Q8, snapshot-pipeline §4/§7, command-pipeline `Handle`), 6 from the Scaffold State/Store types cascade. Mechanical text edits across ~6 docs, then re-lock pile-counter. **§6 boundary redesign deferred to the session after.**

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created
- alignment debt — initial round (6): pile-counter §9.4 (handler ctx.Mutate replacement), §9.5 (mutator payload type + Scaffold base class), Q8 (resolved-now), engine/03 §4 (CommandId in DecisionPrompt), engine/03 §7 (replace placeholder sketch), actions/01 (Handle returns Task<TResult>).
- alignment debt — Scaffold cascade (6): engine/02 (`IGameStateProvider` → `IStateScope` in GameRules ctor), engine/03 (multiple references; `State.Current` → `state.Get<TState>()`), engine ADR 0001 + 0002 (prose updates), pile-counter (heavy: `state.Piles`/`state.LastMove`/`state with {…}` → slice-based reads, `IStateMutator<MoveCardCommand>` → `Mutator<TState, TPayload>`), OPEN-QUESTIONS (3 mentions; promote `GameState` slice catalog).
- new follow-ups in §9 of the Model:
  - Effect authoring & registration recipe (definition-side; out of scope).
  - Effect per-instance state location.
  - `CommandResult` cancellation shape.
  - **`IHandlerContext` vs. constructor-injected services on handlers** — revisit when a second game stresses the surface.
  - **`IDecisionResolver` shape — full review** when FSM-shaped state model lands; not load-bearing meanwhile.
  - Suspension across engine ticks (save/load) — depends on game-lifecycle design (task #12).
  - **`GameState` slice catalog** (task #2) — promoted to load-bearing by the Scaffold binding.
- carried-forward open question: snapshot scope as state machine (FreeInput / DecisionPrompt FSM-shaped successor) remains in OPEN-QUESTIONS.

### Ephemeral questions (scratch, not promoted)
- Concern raised about mixing free-input availability and decision/choice prompts under one `AvailableActions` snapshot — flagged as a watch item for the FSM-shaped state-machine follow-up; deliberately not redesigned now.
- `MoveCardHandler` shape under no-mutate handlers — confirmed handler still has work (validate, choose payload, compute non-trivial result), just not inline mutation.
- Where `Store.Execute` lives interface-wise: Scaffold puts Execute on the concrete `Store`, not on `IStoreScope` — handlers receive the concrete class. Captured in §4.4.

### References
- Docs touched: `features/effects/models/04-effect-flow.md` (created + patched), `ledgers/ALIGNMENT-DEBT.md` (12 entries), `ledgers/FEATURE-MAP.md` (effects row), `ledgers/OPEN-QUESTIONS.md` (struck one), `SESSION-CHECKPOINT.md` (refreshed).
- Scaffold types audited: `com.scaffold.states/Runtime/Abstractions/IStateScope.cs`, `IStoreScope.cs`, `Runtime/Store.cs`, `Runtime/Mutators/Mutator.cs`.
- Related sessions: session-09 (async layer hierarchy settled), session-12 (workflow restructure).
- PR: #17 (working PR for this session, opened during scaffolding).
