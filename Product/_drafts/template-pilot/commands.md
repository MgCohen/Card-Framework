---
type: feature
status: draft
references: [features/triggers/archive/triggers.md, features/state/archive/state.md, features/entities/archive/entities.md]
tags: [#commands]
---

# commands — feature

## What it is

The framework's action-processing pipeline: a structured intent enters the engine, becomes a validated `GameCommand`, runs through trigger folds and a per-command handler, mutates state through mutators, and emits a `HistoryEnvelope` for observers. The pipeline is generic; what each command means is delegated to game-supplied handlers.

```
Intent ─→ Pipeline.Validate ─→ Engine queue ─→ Pipeline.Execute
                                                      │
                                          ┌───────────┴──────────┐
                                          │  OnBefore<T> fold    │  (modify / cancel / replace)
                                          │  Handler.Execute     │  (resolve + invoke mutators)
                                          │  OnAfter<T> fold     │  (reactions)
                                          └───────────┬──────────┘
                                                      ▼
                                              HistoryEnvelope
                                                      │
                                                      ▼
                                                 Observers
```

## Vocabulary

Feature-owned terms (cross-feature terms — `GameCommand`, `GameAction`, `OnBefore<T>`, `OnAfter<T>`, `Mutator` — live in their owning features and `Product/ledgers/GLOSSARY.md`).

| Term | Meaning |
|---|---|
| `ICommandPipeline` | The framework executor that runs one command through validate → before → handler → after → envelope. |
| `ICommandHandler<TCommand, TResult>` | Per-command resolver. One per `GameCommand` type. Game-supplied. |
| `CommandResult` | Typed return from a handler. Populates `HistoryEnvelope.Result`. |
| `HistoryEnvelope` | The grouping unit observers consume — one per `GameAction`, with cause metadata for chains. |

## Architecture

The pipeline is a five-phase procedure executed once per `GameAction`:

1. **Validate** — pipeline resolves the handler from DI and delegates to `handler.Validate`. Side-effect-free; rejection produces no envelope.
2. **Before-fold** — `OnBefore<TCommand>` triggers run via `ITriggerPipeline` (owned by `triggers/`). Each may modify the command, cancel it, or replace it with a different `GameAction`.
3. **Handler.Execute** — handler reads state, computes outcome, invokes mutators (which write state through `state/`), returns a typed `TResult`.
4. **After-fold** — `OnAfter<TCommand>` triggers run, dispatching reaction commands into the queue.
5. **Envelope emission** — pipeline closes the envelope with `Result` and collected entries; observers fan out.

The queue, drain loop, and intake routing are not in this feature — they're owned by `engine/`. `commands/` owns the *per-command* procedure; `engine/` owns the loop *around* it.

## Composition

| Model | Role |
|---|---|
| `models/01-command-handler.md` | `ICommandHandler<TCommand, TResult>` contract |
| `models/02-command-pipeline.md` | The pipeline algorithm itself |
| `models/03-history-envelope.md` | Envelope shape + entry kinds *(stub — not in pilot)* |

## Invariants

Cross-Model rules. Models below reference these by name rather than restating.

- **Single intake.** Every command flows through `Pipeline.Validate`. No handler invocation bypasses it.
- **One handler per command type.** DI registration is closed-generic (`ICommandHandler<DealDamageCommand, DamageResult>`). Multiple registrations for the same `TCommand` are an error at container build.
- **Handlers are the only layer that issues mutators.** Effects sequence commands; mutators write state; handlers bridge.
- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. (Supersedes the old child-folds-into-parent model.)

## Boundaries

**Owns:** `ICommandPipeline`, `ICommandHandler` contract, `HistoryEnvelope` shape, the per-command algorithm.

**Does not own:**
- Command type hierarchy (`GameCommand` / `GameAction` / `CompositeAction` / `GameSignal`) → `triggers/`
- Trigger fold semantics (modify / cancel / replace) → `triggers/`
- Queue, drain loop, intake routing → `engine/`
- Effect orchestration (sequencing commands) → `effects/`
- Mutator implementation, state slices → `state/`
- UI loop, intent mapping, snapshot → `presentation/`

## Dependencies

*Mirrors the front-matter `references:` array in human-readable form (per §3).*

- **Upstream:** `triggers/` (command type hierarchy), `state/` (`Mutator`, `IStateScope`), `entities/` (instance ids).
- **Downstream:** `engine/` (uses pipeline), `effects/` (dispatches via scope, awaits results), `presentation/` (consumes envelopes).

## Open

See `ledgers/OPEN-ITEMS.md`:
- `oq-decision-handler-shape` — special-case handler that awaits player input.
- `oq-history-envelope-replay-shape` — is the envelope the canonical replay record?
