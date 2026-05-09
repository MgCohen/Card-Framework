---
type: model
status: settled
references: [features/actions/models/02-intent-mapper.md, features/actions/models/03-command-handler.md, features/actions/models/04-history-envelope.md, features/triggers/models/01-trigger-fold.md, features/effects/models/02-effect-scope.md, features/engine/models/03-game-rules-runtime.md, decisions/0006-two-pipeline-architecture.md, decisions/0007-five-validation-layers.md]
tags: [#actions, #model, #pipeline]
---

# Command Pipeline — model

## What it is

The dispatch mechanic: how a `GameCommand` flows through `engine.Dispatch` — lifecycle gate, validate, enqueue, drain (before-fold → handler → after-fold → envelope) — and how the queue is drained framework-FIFO per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md).

```text
engine.Dispatch(command)
  → engine lifecycle gate (framework)
  → pipeline.Validate (handler.Validate — authoritative)
  → enqueue (framework FIFO)
  → drain:
      → before-fold (OnBefore<T>: modify / cancel / replace)
      → handler.Handle (read state, invoke mutators, return CommandResult)
      → after-fold (OnAfter<T>: dispatch reactions)
      → envelope publishes
  → GameRulesRuntime.Evaluate (state-based follow-ups + snapshot publication)
```

The pipeline owns the *flow*; the surrounding load-bearing types live in their own models — `IIntentMapper` ([`02`](features/actions/models/02-intent-mapper.md)), `ICommandHandler` ([`03`](features/actions/models/03-command-handler.md)), `HistoryEnvelope` ([`04`](features/actions/models/04-history-envelope.md)), `AvailableActionsSnapshot` ([`05`](features/actions/models/05-available-actions-snapshot.md)).

## Contracts

### `GameCommand` — type hierarchy

Defined in [`triggers/01-trigger-fold.md` §The type hierarchy](features/triggers/models/01-trigger-fold.md):

```csharp
abstract record GameCommand;                     // base — all pipeline-dispatchable types
abstract record GameAction      : GameCommand;   // executable, modifiable, queued through engine
abstract record CompositeAction : GameCommand;   // reusable StateMutation group, runs inline within a handler
abstract record GameSignal      : GameCommand;   // not executable — trigger surface only
```

| Type | Executed by | Modifiable in before-fold | Goes through engine queue |
|---|---|---|---|
| `GameAction` | Handler, via engine queue | Yes | Yes |
| `CompositeAction` | Inline, within parent handler | No | No |
| `GameSignal` | Nothing — no execution step | No | No |

A handler should never dispatch another `GameAction` directly — that's an `Effect`'s responsibility (per [`effects/01-effect.md`](features/effects/models/01-effect.md)).

### `ICommandPipeline`

```csharp
public interface ICommandPipeline
{
    bool Validate(GameCommand cmd, IStateScope state, out string? reason);
    Task<CommandResult> Execute(GameAction action);
}
```

Framework executor: resolves the registered `ICommandHandler` for the command type, runs validation, drives the drain steps. Game authors do not implement this; they register handlers (per [`03-command-handler.md`](features/actions/models/03-command-handler.md)).

### Pipeline mechanics

Per `engine.Dispatch(command)`:

1. **Engine lifecycle gate** (framework). Reject pre-`Started` (except `StartMatchCommand` / `EndMatchCommand`) and post-`Ended`. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), this is the framework-owned flow-level gate; the runtime no longer participates in intake routing.
2. **`pipeline.Validate`** (framework). Resolves the handler from the registry and delegates to `handler.Validate(command, state, out reason)`. **Authoritative legality check** (per [ADR-0007](Product/decisions/0007-five-validation-layers.md)).
3. **Enqueue** (framework). Command goes onto the framework FIFO queue.
4. **Drain** (framework, when queue is non-empty and no drain is in progress):
   1. Dequeue.
   2. **Before-fold** — `ITriggerPipeline.RunBefore` runs `OnBefore<T>` triggers in priority order (per [`triggers/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md)). Triggers may modify, cancel (`scope.Cancel`), or replace (`scope.Replace`). On replace: original envelope closes with `Replaced`; replacement dispatches as a fresh command through the channel.
   3. **`handler.Handle(command, ctx)`** (game). Reads state, decides outcome, calls `ctx.Store.Execute(payload)` to fan out to registered mutators, returns `Task<TResult>`.
   4. **After-fold** — `ITriggerPipeline.RunAfter` runs `OnAfter<T>` triggers. Reactions dispatched via `engine.Dispatch` (separate envelopes; framework FIFO).
   5. **Envelope publishes** with `CommandResult` (per [`04-history-envelope.md`](features/actions/models/04-history-envelope.md)).
5. **`GameRulesRuntime.Evaluate`** (game). Returns 0..N follow-up commands for the engine to enqueue (with threaded `OriginCommandId`). Snapshot publication (`Actions.Set`) folds into `Evaluate` per ADR-0006 — there is no separate `OnQueueDrained` callback.
6. Repeat until queue empties.

### Relationship to triggers

Per [`triggers/01-trigger-fold.md`](features/triggers/models/01-trigger-fold.md):

- `OnBefore<T>` triggers run before the handler. They may modify the command, cancel it (`scope.Cancel`), or replace it (`scope.Replace`).
- `OnAfter<T>` triggers run after the handler. They may dispatch reactions through the channel (`engine.Dispatch`).
- The trigger pipeline runs as part of `pipeline.Execute` — handler authors don't see the fold directly.

### Relationship to effects

Per [`effects/01-effect.md`](features/effects/models/01-effect.md):

- An `Effect` orchestrates multiple `GameAction`s by calling `scope.Dispatch(action)` (immediate, inline, await result).
- Effects never mutate state directly; only handlers invoke mutators.

## Invariants

- **One enforced entry point.** Every `GameCommand` enters the pipeline through `engine.Dispatch`. No bypass paths; no in-process injection.
- **Snapshot is approximate; dispatch is authoritative.** Per-binding `Validate` runs at dispatch (always) and may run during snapshot enumeration (on-demand by the rule). UI gates may show stale bindings; dispatch catches them.
- **Command-queue routing is framework-FIFO.** Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md), the runtime does not gate intake or route commands; game-style differences in how reactions are scheduled live at the effect-level orchestration layer.
- **`ActionTemplate` is the snapshot wire-form.** `Ref<T>` and `Reference` fields on templates are boundary-correct (per [ADR-0005](Product/decisions/0005-objects-over-ids.md)) — they're transported to presentation and consumed by `IntentMapper`.

## Decisions

Linked: [`Product/decisions/0006-two-pipeline-architecture.md`](Product/decisions/0006-two-pipeline-architecture.md) — framework command pipeline + game effect pipeline split. Per ADR-0006, command-queue routing is framework-FIFO; the runtime does not gate intake or route commands; effect-level orchestration is the customizable layer where game styles diverge.

Linked: [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — five validation layers; `handler.Validate` authoritative (per [`03-command-handler.md`](features/actions/models/03-command-handler.md)).

## Examples

### Pile Counter — a complete dispatch

```text
Player drags top card of Pile A onto Pile B
  → IntentGateway.Submit(MoveAttempt, MoveCardTemplate(card, [pileB]), choice)
  → IIntentMapper.Map(MoveCardTemplate, choice) → MoveCardCommand(card, A, B)
  → engine.Dispatch(MoveCardCommand)
     ├─ Engine lifecycle gate: Started → permitted
     ├─ pipeline.Validate → MoveCardHandler.Validate(card, A, B) → ok
     └─ enqueued (framework FIFO)

Drain begins:
  ├─ Dequeue MoveCardCommand
  ├─ envelope opens (OriginCommandId = newGuid, OriginIntent = MoveAttempt)
  ├─ ITriggerPipeline.RunBefore → no triggers registered → fold continues
  ├─ MoveCardHandler.Handle:
  │    ctx.Store.Execute(new MoveCardPayload(card, A, B))
  │      → PileMoveMutator.Change → new PilesState (card moved A→B)
  │      → LastMoveMutator.Change → new LastMoveState(card, A, B)
  │      → atomic commit
  │    return new MoveCardResult(card, A, B)
  ├─ ITriggerPipeline.RunAfter → no triggers → fold continues
  └─ envelope publishes (per 04-history-envelope.md)

GameRulesRuntime.Evaluate(MoveCardCommand, MoveCardResult):
  → All piles match target? → return [new EndMatchCommand(Win(player1))]
  → publish updated snapshot via Actions.Set(...)
  → enqueue follow-up with shared OriginCommandId

(continues until queue empty)
```

The envelope is the unit observers consume. Triggers are fold steps in the same execution. Mutators are pure functions of `(currentState, payload, scope)` returning new state. Snapshot publication folds into `Evaluate` per ADR-0006.

## Open follow-ups

- **Scheduler models** — broader scheduling design (interrupt injection, priority queues, MTG-stack semantics) lives at the effect-level orchestration layer per ADR-0006; intersects this pipeline indirectly.

---

*Below: provenance — feature-internal decisions folded on 2026-05-08; element-specific Qs (validation authority, IntentMapper, vocabulary rename) extracted to sibling models on 2026-05-09. The two Qs that remain here are the pipeline-level decisions: flow-level intake (Q1) and command catalog shape (Q2).*

## Options considered

### Q1 — `OnIncomingCommand` is purely flow (formerly ADR-0003 — superseded then folded)

> **Status: superseded by [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) (2026-05-07).** `OnIncomingCommand` itself is **deleted**. Walking through Stacklands / Hearthstone / MTG-like game styles concretely showed that command-queue routing isn't what games actually customize — what differs is *effect-level* orchestration (when each effect starts, whether effects nest / batch / stack). Per-command primitives (modify / cancel / replace / dispatch) are framework-level via `IEffectScope` during the trigger fold; pause/end gating moved to the framework lifecycle gate on `engine.Dispatch`; game-flow legality lives in `pipeline.Validate`. The "purely flow" principle this ADR locked is preserved in spirit — the framework still distinguishes flow-level concerns from per-command body inspection — but the named hook is gone. The original options-considered analysis is preserved below as historical record per P14.

`OnIncomingCommand` initially mixed three concerns: pause/end gating, queue routing, and command-body inspection. This forced the rule to know about every command shape and made composition fragile.

- **A — Allow `OnIncomingCommand` to read command bodies for early validation.** Rejected — duplicates handler-side validation; couples the rule to every command type.
- **B — Replace `OnIncomingCommand` with per-command callbacks.** Rejected — loses the flow-level concerns (pause/end).
- **C — `OnIncomingCommand` is purely flow: pause/end gating + queue routing; never reads the body.** **Chosen at the time** (now superseded — see ADR-0006).

### Q2 — Generic command primitives, rule-enforced constraints (formerly ADR-0005 — folded)

Specialized commands like `MoveTopCardCommand` accumulate quickly: top-card, bottom-card, random-card, n-cards, etc. Each one duplicates `MoveCardCommand` plus a constraint baked into the type. This explodes the command catalog.

- **A — Ship specialized commands per common pattern.** Rejected — combinatorial explosion of command types.
- **B — Ship one parameterized command** (`MoveCardCommand` with a "selector" enum). Rejected — the selector becomes a stringly-typed sub-language inside the command.
- **C — Ship generic primitives (`MoveCardCommand`); rules enforce constraints via per-template eligibility and `Validate`.** **Chosen.** "Top card", "random card", "from hand only" are constraints expressed by the rule when constructing templates and validated in handlers.

## Rationale

Q1's `OnIncomingCommand`-is-flow keeps the rule shape-agnostic at intake; body inspection belongs to the per-command handler. The named hook itself was deleted by ADR-0006, but the principle (flow-level concerns separate from body inspection) was preserved. Q2's generic primitives keep the command catalog small and stable; constraints live where the game already knows them.

## Consequences

- **Enables:** simple, command-shape-agnostic flow check via the framework lifecycle gate (Q1, post-supersession); small stable command catalog (Q2).
- **Constrains:** rule cannot reject by body at intake (Q1, Q2 push body checks to handlers); rules carry constraint logic that lives in the right place (Q2).
- **Reverts/replaces:** `OnIncomingCommand` deleted per ADR-0006 (Q1).
- **Cascade:** consumed by `engine/03-game-rules-runtime.md` (`GameRulesRuntime` two-method contract: `FilterTriggers` + `Evaluate`); `effects/01-effect.md` (handlers don't dispatch `GameAction`s — sequencing belongs to effects).
