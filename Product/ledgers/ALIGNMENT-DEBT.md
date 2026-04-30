---
type: ledger
last-updated: 2026-04-30
---

# Alignment Debt

Single source of truth for cascade implications from Model edits. One entry per `(source-edit, affected-doc)` pair. See `_workflow/PRODUCT-BUILDER.md` §5.7 for opening rules and §5.8 for the discharge threshold.

**Entry shape** (see proposal §6 Ledger entry shapes):

```markdown
- YYYY-MM-DD | source: <path> | target: <path> | reason: <one line> | open
```

Resolved entries get strikethrough plus resolution date and link to the resolving commit/PR:

```markdown
- ~~YYYY-MM-DD | source: ... | target: ... | reason: ... | open~~ → resolved YYYY-MM-DD ([commit](link))
```

---

## Open

- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md §9.4 | reason: handler uses `ctx.Mutate.MoveCardBetweenPiles(...)`; new model removes `Mutate` from `IHandlerContext` — replace with `ctx.Store.Execute<TMutator, TPayload>(payload)` calls | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md §9.5 | reason: `LastMoveMutator : IStateMutator<MoveCardCommand>` uses the command as payload; new model requires dedicated payload types — switch to `IStateMutator<LastMovePayload>` (and add `PileMoveMutator` invocation in handler) | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: support/samples/pile-counter.md Q8 | reason: `IHandlerContext` shape resolved (Store / Actions / Resolver, no Mutate); update Q8 from "placeholder" to resolved with link to 04-effect-flow §4.4 | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: features/engine/models/03-snapshot-pipeline.md §4 | reason: `DecisionPrompt(DecisionId Id)` should be `DecisionPrompt(CommandId Id)`; the decision-id-≡-command-id collapse retires the parallel `DecisionId` type | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: features/engine/models/03-snapshot-pipeline.md §7 | reason: §7 sketches `ctx.Actions`, `ctx.DecisionId`, `ctx.AwaitPlayerChoice()` as placeholders; replace with the canonical surface (`ctx.Actions.Set`, `ctx.Resolver.Await(action.Id)`) and link to 04-effect-flow | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md | target: features/actions/models/01-command-pipeline.md | reason: `ICommandHandler.Handle` now returns `Task<TResult>` instead of `TResult`; update interface signature and any handler examples | open

### Cascade — Scaffold State/Store types audit

Driven by §4.1–§4.2 of `04-effect-flow.md`. Several docs invented `IGameStateProvider`, `IStore`, `IStateMutator<TPayload>`, and a single-record `GameState` (`state with { ... }`, `state.Piles`, `state.LastMove`). Scaffold's actual types are: `IStateScope` (read), `IStoreScope : IStateScope` (read + events), `Store` (concrete; `Execute<TPayload>(payload)` fans out to all mutators registered for that payload type), `Mutator<TState>` / `Mutator<TState, TPayload>` (with `Change(state, [payload], IStateScope scope)`). State is sliced — accessed via `store.Get<TState>()`, not as a single record. `GameState` shape is still an open question (slice catalog).

- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/models/02-gamerules-contract.md | reason: `GameRules` constructor takes `IGameStateProvider state`; replace with Scaffold `IStateScope`. Remove the invented `IGameStateProvider` definition; reference Scaffold types directly. | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/models/03-snapshot-pipeline.md | reason: Multiple references to `IGameStateProvider` (constructor signatures, §5 service table, §9 open items); replace with `IStateScope`. Update `State.Current` reads to `state.Get<TState>()`. | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/decisions/0001-gamerules-as-class-with-services.md | reason: ADR mentions `IGameStateProvider` as the read-state service; rephrase to point at Scaffold `IStateScope` (decision intent unchanged — services-injected — only the type name changes). | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: features/engine/decisions/0002-gamerules-zero-state.md | reason: Same as above — references `IGameStateProvider` in supporting prose; swap to `IStateScope`. | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: support/samples/pile-counter.md | reason: Heavy usage of single-record `GameState` (`state.Piles`, `state.LastMove`, `state with { ... }`), an invented `IStateMutator<MoveCardCommand>`, and `LastMoveMutator : IStateMutator<MoveCardCommand>`. Switch to: slice-based reads (`scope.Get<PileState>(pileRef)`, `scope.Get<LastMoveState>()`); Scaffold `Mutator<TState, TPayload>` with payload-keyed registration; handler does one `ctx.Store.Execute(new MoveCardPayload(...))` and the registered `PileMoveMutator` and `LastMoveMutator` both fan out from that payload. Discharges in lockstep with the §9.4–§9.5 + Q8 entries above. | open
- 2026-04-30 | source: features/effects/models/04-effect-flow.md §4 | target: ledgers/OPEN-QUESTIONS.md | reason: Three `IGameStateProvider` mentions (in resolved-but-still-listed items and the snapshot-scope follow-up); replace with `IStateScope`. The `GameState` contract / slice catalog open item is the load-bearing one — promote to track the slice catalog explicitly. | open

## Resolved

_(none yet)_
