---
type: ledger
schema-version: 2
last-updated: 2026-05-01
---

# Alignment Debt

Single source of truth for cascade implications from Model edits. One entry per `(source-edit, affected-doc)` pair. See `_workflow/PRODUCT-BUILDER.md` §5.7 for opening rules and §5.8 for the discharge threshold.

**Entry shape (v2):**

```markdown
- id: ad-YYYYMMDD-NNN | status: open | source: <path> | target: <path> | reason: <one line>
```

Resolved entries get strikethrough plus resolution note:

```markdown
- ~~id: ad-YYYYMMDD-NNN | status: open | source: ... | target: ... | reason: ...~~ → resolved YYYY-MM-DD (note)
```

---

## Entries

### Linter findings — 2026-05-01

- id: ad-20260501-026 | status: open | source: linter | target: features/engine/models/_test-fixture.md | reason: S2 — builds-on references missing or archived path (features/engine/models/DOES-NOT-EXIST.md)

### Cascade — engine wiring stack design pass + patches (session-20260501-1132)

- id: ad-20260501-022 | status: open | source: features/engine/models/02-gamerules-contract.md | target: features/engine/models/03-snapshot-pipeline.md | reason: GameRules sketch still shows 4-service constructor with `IHandlerRegistry Handlers`; Q2 verdict removed it — update to 3 services
- id: ad-20260501-023 | status: open | source: features/engine/models/02-gamerules-contract.md | target: features/engine/decisions/0001-gamerules-as-class-with-services.md | reason: ADR option C lists `IHandlerRegistry` as an injected service; now 3 services after Q2 verdict
- id: ad-20260501-024 | status: open | source: features/engine/models/02-gamerules-contract.md | target: ledgers/OPEN-QUESTIONS.md | reason: oq-handler-registry, oq-command-pipeline, oq-engine-startup, oq-gamerules-service-wiring, oq-trigger-type-unification all resolved session-20260501-1132 — strike through
- id: ad-20260501-025 | status: open | source: features/triggers/models/01-trigger-fold.md | target: features/effects/models/02-replacement-prevention.md | reason: ITriggerPipeline shape conflict — effects/02 §3 has `Task<TriggerFoldResult> RunBefore(GameCommand, IReadOnlyList<TriggerMatch>, IEffectScope)`; triggers/01 §7 has `BeforeFoldResult RunBefore(GameAction, IEffectScope)` — reconcile to one authoritative shape

### Cascade — S19 slice catalog + Effect/Trigger unification

- ~~2026-05-01 | id: ad-20260501-001 | source: features/engine/models/04-gamestate-slices.md | target: support/samples/pile-counter.md | reason: §9.8 `BuildInitialSlices` explicitly yields `AvailableActionsState`; engine now auto-seeds framework slices at `Build()` — remove from `BuildInitialSlices` | open~~ → resolved 2026-05-01 (AvailableActionsState removed; comment added pointing to 04-gamestate-slices §4)
- ~~2026-05-01 | id: ad-20260501-002 | source: features/engine/models/04-gamestate-slices.md | target: features/engine/models/01-evaluation-loop.md | reason: §3 `GameEngine` sketch holds `private readonly GameState state` field; no single-record `GameState` exists — field should reference `IStateScope` or `Store` | open~~ → resolved 2026-05-01 (`GameState state` replaced with `Store store`)
- ~~2026-05-01 | id: ad-20260501-003 | source: features/triggers/models/01-trigger-fold.md | target: features/effects/models/02-replacement-prevention.md | reason: `FoldResult` was defined here as the public `Keep/Cancel/Replace` discriminated union returned by react functions; it is now pipeline-internal only — update to reflect that game authors use `scope.Cancel()`/`scope.Replace()` instead | open~~ → resolved 2026-05-01 (§1 updated to Trigger<T>/scope API; §3 TriggerMatch delegates use IEffectScope; §4 GameRules shape aligned; §6 prevention example rewritten)

### Cascade — engine/02 contract settled → engine/01 and triggers/01 (S17 audit → discharged S18)

Auto debt-pass: engine/02's 4-method `GameRules` shape, `IntakeResult`/`DispatchOrigin`/`DrainResult` types flowed to engine/01 and triggers/01. Signatures updated; inline GameRules interfaces and old type references removed.

- ~~2026-05-01 | id: ad-20260501-004 | source: features/engine/models/02-gamerules-contract.md | target: features/engine/models/01-evaluation-loop.md §3 §5 §6 | reason: old `void OnQueueDrained(GameEngine,GameState)`, `Evaluate(...,GameState state)`, `SchedulingDecision/SchedulingContext` signatures; engine/02 settled `DrainResult OnQueueDrained()`, `Evaluate(action,result)`, `IntakeResult/DispatchOrigin` | open~~ → resolved 2026-05-01 (auto debt-pass: signatures updated in Dispatch, Drain, interface definitions; DispatchOrigin and IntakeResult/DrainResult types added)
- ~~2026-05-01 | id: ad-20260501-005 | source: features/engine/models/02-gamerules-contract.md | target: features/triggers/models/01-trigger-fold.md §4 | reason: §4 shows an inline 3-method `GameRules` interface with `SchedulingDecision` return and no `FilterTriggers`; engine/02 settled the 4-method abstract-class shape | open~~ → resolved 2026-05-01 (auto debt-pass: inline interface updated to 4-method settled shape with FilterTriggers)

### Cascade — effects/02 FoldResult settled → triggers/01 react signature (S17 audit → discharged S18)

Auto debt-pass: effects/02's `FoldResult` discriminated union flowed to triggers/01. Old `Func<T,Context,T>` react signatures replaced with `Func<T,Context,FoldResult>` throughout §2 and §4.

- ~~2026-05-01 | id: ad-20260501-006 | source: features/effects/models/02-replacement-prevention.md | target: features/triggers/models/01-trigger-fold.md §2 | reason: triggers/01 §2 declares `react: Func<T,Context,T>`; effects/02 settled `react: Func<T,Context,FoldResult>` (discriminated union Keep/Cancel/Replace); incompatible — one of the two must be the authoritative shape | open~~ → resolved 2026-05-01 (auto debt-pass: react signatures and example returns updated to FoldResult; Keep/Cancel/Replace variants added to §2)

### Cascade — Scaffold types (session 14) — IHandlerRegistry.Validate (S17 audit → discharged S18)

Auto debt-pass: Scaffold's `IStateScope` flowed to `IHandlerRegistry` interface. `GameState state` parameter replaced with `IStateScope state` in §6 of engine/02.

- ~~2026-05-01 | id: ad-20260501-007 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/models/02-gamerules-contract.md §6 | reason: `IHandlerRegistry.Validate` still takes `GameState state`; Scaffold cascade (S14) retired single-record GameState in favour of `IStateScope`; missed in S14 discharge | open~~ → resolved 2026-05-01 (auto debt-pass: interface signature updated to `IStateScope`)

### Cascade — session 15/16 — Scope references (S17 audit → discharged S18)

Auto debt-pass: S15 Q6 `Scope` discriminator deletion flowed through. `FreeInput()` Scope arguments removed from `AvailableActionsSnapshot` constructor calls in presentation/01 and pile-counter.

- ~~2026-05-01 | id: ad-20260501-008 | source: sessions/session-15.md | target: features/presentation/models/01-presentation-loop.md §12 | reason: §12 still references `Scope` argument on `AvailableActionsSnapshot` constructor; Scope discriminator deleted S15 Q6 | open~~ → resolved 2026-05-01 (auto debt-pass: Scope/FreeInput reference removed from code example)
- ~~2026-05-01 | id: ad-20260501-009 | source: sessions/session-15.md | target: support/samples/pile-counter.md §9.1 | reason: §9.1 last paragraph still mentions `Scope` field on `AvailableActionsSnapshot`; same S15 Q6 deletion; missed in S15–S16 discharge | open~~ → resolved 2026-05-01 (auto debt-pass: Scope reference removed from rule example; EntityId → PileId cast corrected)

### Cascade — §6 boundary redesign (session 15 design pass → discharged session 16)

Driven by S15 Q1–Q8 verdicts. `IDecisionResolver` retired in favour of `ICommandAwaiter` (type-only routing, no id, closure-captured `cmd is T`). `SnapshotScope` / `FreeInput` / `DecisionPrompt` removed from the snapshot (Q6: scope discriminator dropped; awaiter answers "is an await open?"). `DecisionResponseCommand` / `DecisionResponseHandler` deleted. Engine-level pre-pipeline awaiter check replaces mapper-side scope branching. Mapper and gateway are decision-unaware. ADR-0006 + ADR-0007 restored.

- ~~2026-05-01 | id: ad-20260501-010 | source: sessions/session-15.md | target: features/effects/models/04-effect-flow.md §4.5 | reason: `IDecisionResolver` → `ICommandAwaiter`; interface now `Wait<TCommand>(ct)` + `TryResolve(cmd)`; type-only routing, no id | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-011 | source: sessions/session-15.md | target: features/effects/models/04-effect-flow.md §5 | reason: §5.2 DecisionPrompt deleted (no scope); §5.1 reframed as no-id / type-only; §5.4 end-to-end thread rewritten | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-012 | source: sessions/session-15.md | target: features/effects/models/04-effect-flow.md §6 | reason: Remove `DecisionResponseCommand`/`Handler` + scope-switch mapper; add engine-level awaiter check + decision-unaware gateway | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-013 | source: sessions/session-15.md | target: features/effects/models/04-effect-flow.md §10 §11 | reason: Invariants 1/2/3/6 updated for no-id / `ICommandAwaiter` / awaiter-check boundary; §11 links | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-014 | source: sessions/session-15.md | target: features/engine/models/03-snapshot-pipeline.md §4 | reason: Remove `SnapshotScope` / `FreeInput` / `DecisionPrompt`; snapshot is templates-only | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-015 | source: sessions/session-15.md | target: features/engine/models/03-snapshot-pipeline.md §7 | reason: `RequestTargetHandler` example: drop `DecisionPrompt` from `Actions.Set`; `Await(id)` → `Wait<SelectTargetCommand>()` | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-016 | source: sessions/session-15.md | target: features/engine/models/03-snapshot-pipeline.md §9 | reason: Snapshot-scope FSM open item reframed; discriminator deleted, not transitional | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-017 | source: sessions/session-15.md | target: features/actions/models/01-command-pipeline.md §3 | reason: Gateway confirmation note: decision-unaware per ADR-0006 restored; cross-reference 04-effect-flow | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-018 | source: sessions/session-15.md | target: features/actions/models/02-availability-snapshot.md | reason: Remove `FreeInput` from `AvailableActionsSnapshot` calls; note scope dropped in §4 of doc 29 | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-019 | source: sessions/session-15.md | target: features/presentation/models/01-presentation-loop.md | reason: Remove Scope prose in §4/§11/§12; add awaiter pre-check to §10 step 8; update §13 async-decisions caveat | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-020 | source: sessions/session-15.md | target: support/samples/pile-counter.md | reason: Mapper comment; remove `new FreeInput()` from `Actions.Set` calls in §9.3/§9.6/§9.8 | open~~ → resolved 2026-05-01 (session 16)
- ~~2026-05-01 | id: ad-20260501-021 | source: sessions/session-15.md | target: ledgers/OPEN-QUESTIONS.md | reason: Snapshot-scope-as-FSM entry reframed; `FreeInput`/`DecisionPrompt` deleted (not transitional); new framing is awaiter succession | open~~ → resolved 2026-05-01 (session 16)

### Cascade — Scaffold State/Store types audit (session 14)

Driven by §4.1–§4.2 of `04-effect-flow.md`. Several docs invented `IGameStateProvider`, `IStore`, `IStateMutator<TPayload>`, and a single-record `GameState`. Scaffold's actual types are: `IStateScope` (read), `IStoreScope : IStateScope` (read + events), `Store` (concrete; `Execute<TPayload>(payload)` fans out to all mutators registered for that payload type), `Mutator<TState>` / `Mutator<TState, TPayload>`. State is sliced — accessed via `store.Get<TState>()`.

_All entries discharged in session 14._

- ~~2026-04-30 | id: ad-20260430-001 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md §9.4 | reason: handler uses `ctx.Mutate.MoveCardBetweenPiles(...)`; new model removes `Mutate` from `IHandlerContext` — replace with `ctx.Store.Execute<TMutator, TPayload>(payload)` calls | open~~ → resolved 2026-04-30 (session 14: handler now does single `ctx.Store.Execute(new MoveCardPayload(...))`)
- ~~2026-04-30 | id: ad-20260430-002 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md §9.5 | reason: `LastMoveMutator : IStateMutator<MoveCardCommand>` uses the command as payload; new model requires dedicated payload types — switch to `IStateMutator<LastMovePayload>` (and add `PileMoveMutator` invocation in handler) | open~~ → resolved 2026-04-30 (session 14: `PileMoveMutator` and `LastMoveMutator` both `Mutator<TState, MoveCardPayload>`; payload-keyed fanout)
- ~~2026-04-30 | id: ad-20260430-003 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md Q8 | reason: `IHandlerContext` shape resolved (Store / Actions / Resolver, no Mutate); update Q8 from "placeholder" to resolved with link to 04-effect-flow §4.4 | open~~ → resolved 2026-04-30 (session 14: Q8 struck through with link to 04-effect-flow §4.4)
- ~~2026-04-30 | id: ad-20260430-004 | source: features/effects/models/04-effect-flow.md | target: features/engine/models/03-snapshot-pipeline.md §4 | reason: `DecisionPrompt(DecisionId Id)` should be `DecisionPrompt(CommandId Id)`; the decision-id-≡-command-id collapse retires the parallel `DecisionId` type | open~~ → resolved 2026-04-30 (session 14: `DecisionPrompt(CommandId Id)` with cross-link to 04-effect-flow §5.1)
- ~~2026-04-30 | id: ad-20260430-005 | source: features/effects/models/04-effect-flow.md | target: features/engine/models/03-snapshot-pipeline.md §7 | reason: §7 sketches `ctx.Actions`, `ctx.DecisionId`, `ctx.AwaitPlayerChoice()` as placeholders; replace with the canonical surface (`ctx.Actions.Set`, `ctx.Resolver.Await(action.Id)`) and link to 04-effect-flow | open~~ → resolved 2026-04-30 (session 14: §7 rewritten with canonical handler example using `ctx.Resolver.Await(action.Id)`)
- ~~2026-04-30 | id: ad-20260430-006 | source: features/effects/models/04-effect-flow.md | target: features/actions/models/01-command-pipeline.md | reason: `ICommandHandler.Handle` now returns `Task<TResult>` instead of `TResult`; update interface signature and any handler examples | open~~ → resolved 2026-04-30 (session 14: Shape 7 description updated; `PlayCardHandler`/`HealActorHandler` examples return `Task.FromResult(...)`)
- ~~2026-04-30 | id: ad-20260430-007 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/models/02-gamerules-contract.md | reason: `GameRules` constructor takes `IGameStateProvider state`; replace with Scaffold `IStateScope`. Remove the invented `IGameStateProvider` definition; reference Scaffold types directly. | open~~ → resolved 2026-04-30 (session 14: constructor takes `IStateScope`; `State.Current` → `State.Get<TState>()`; §10 open item folded)
- ~~2026-04-30 | id: ad-20260430-008 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/models/03-snapshot-pipeline.md | reason: Multiple references to `IGameStateProvider` (constructor signatures, §5 service table, §9 open items); replace with `IStateScope`. Update `State.Current` reads to `state.Get<TState>()`. | open~~ → resolved 2026-04-30 (session 14: §5 constructor + service table + §6 sample + §9 all updated to Scaffold `IStateScope`)
- ~~2026-04-30 | id: ad-20260430-009 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/decisions/0001-gamerules-as-class-with-services.md | reason: ADR mentions `IGameStateProvider` as the read-state service; rephrase to point at Scaffold `IStateScope` (decision intent unchanged — services-injected — only the type name changes). | open~~ → resolved 2026-04-30 (session 14: option C now names Scaffold `IStateScope`)
- ~~2026-04-30 | id: ad-20260430-010 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/decisions/0002-gamerules-zero-state.md | reason: Same as above — references `IGameStateProvider` in supporting prose; swap to `IStateScope`. | open~~ → resolved 2026-04-30 (session 14: Decision/Context now names Scaffold `IStateScope` and slice-based reads)
- ~~2026-04-30 | id: ad-20260430-011 | source: features/effects/models/04-effect-flow.md §4 | target: support/samples/pile-counter.md | reason: Heavy usage of single-record `GameState` (`state.Piles`, `state.LastMove`, `state with { ... }`), an invented `IStateMutator<MoveCardCommand>`, and `LastMoveMutator : IStateMutator<MoveCardCommand>`. Switch to: slice-based reads (`scope.Get<PileState>(pileRef)`, `scope.Get<LastMoveState>()`); Scaffold `Mutator<TState, TPayload>` with payload-keyed registration; handler does one `ctx.Store.Execute(new MoveCardPayload(...))` and the registered `PileMoveMutator` and `LastMoveMutator` both fan out from that payload. Discharges in lockstep with the §9.4–§9.5 + Q8 entries above. | open~~ → resolved 2026-04-30 (session 14: §9.1–§9.8 rewritten on slice/payload model; sample status downgraded to `draft` for re-lock cooldown)
- ~~2026-04-30 | id: ad-20260430-012 | source: features/effects/models/04-effect-flow.md §4 | target: ledgers/OPEN-QUESTIONS.md | reason: Three `IGameStateProvider` mentions (in resolved-but-still-listed items and the snapshot-scope follow-up); replace with `IStateScope`. The `GameState` contract / slice catalog open item is the load-bearing one — promote to track the slice catalog explicitly. | open~~ → resolved 2026-04-30 (session 14: `IGameStateProvider` mentions purged; new "GameState slice catalog" entry promoted)
- ~~2026-04-30 | id: ad-20260430-013 | source: features/effects/models/04-effect-flow.md §4 | target: features/presentation/models/01-presentation-loop.md §10 | reason: end-to-end trace step 10 says `MoveCardBetweenPiles mutator … LastMoveMutator writes state.LastMove` — pre-cascade language. Missed in the session-13 cascade walk. Update to payload-keyed Scaffold mutators writing slices. | open~~ → resolved 2026-04-30 (session 14: step 10 rewritten; opened-and-closed in same pass-mode session)
