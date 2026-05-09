---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/actions/models/03-command-handler.md, features/state/state.md]
tags: [#actions, #model, #history]
---

# History Envelope — model

## What it is

`HistoryEnvelope` is the grouping unit observers consume — one per `GameAction`, carrying cause metadata + entries + result. The `HistoryState` slice holds the ordered envelope log.

```text
HistoryEnvelope
  envelopeId
  originCommandId       // shared across all commands in a chain
  originIntent          // non-null only on the first command in a chain
  actor
  sourceInstance
  rootCommand
  result
  entries[]
```

## Contracts

### Envelope shape

| Field | Meaning |
|---|---|
| `envelopeId` | Unique identifier for this envelope. |
| `originCommandId` | ID of the first command in a chain. Threaded by the engine onto every evaluator follow-up so all chain members share identity. |
| `originIntent` | The originating `PlayerIntent` recorded on the first envelope of a chain only. Subsequent envelopes share `OriginCommandId` but carry no `OriginIntent`. |
| `actor` | The actor that caused the action. |
| `sourceInstance` | The entity that sourced the action (card, ability, attachment). |
| `rootCommand` | The dispatched `GameCommand`. |
| `result` | The handler's `CommandResult`. |
| `entries[]` | The state-change log for this action. |

### Entries

Entries might include: instance created, instance destroyed, zone content changed, variable changed, modifier added/removed, decision opened, command cancelled, command replaced, visibility changed.

The envelope lets observers understand cause:

```text
Player1 used Fireball
  → Card moved hand → stack
  → Cost spent
  → Damage dealt to Enemy2
  → Enemy2 health changed
  → Card moved stack → discard
```

Without an envelope, UI sees unrelated state changes. With an envelope, UI can animate and explain the whole sequence.

### `HistoryState` slice

Closed envelopes accumulate in a framework-owned `BaseState` slice — the canonical home for the envelope log.

```csharp
public sealed record HistoryState(
    IReadOnlyList<HistoryEnvelope> Envelopes) : BaseState;
```

- **Purpose.** Holds the ordered envelope log. Load-bearing for replay, multiplayer transport, and the presentation history loop.
- **Written by.** Engine-internal history mutator (fires after every `pipeline.Execute` call, appending the closed envelope). Not writable by game authors.
- **Read by.** Presentation layer (history loop), networking (transport), replay tooling.
- **Seeded.** `HistoryState(ImmutableList<HistoryEnvelope>.Empty)` at `Build()` (framework auto-seeds; never declared in `MatchSetup.InitialSlices`).

The slice is a framework-owned tier slice — see [`state/state.md` §Slice tiers](features/state/state.md) for the framework-vs-game-authored convention.

## Invariants

- **One envelope per `GameAction`.** Effect-dispatched siblings share `OriginCommandId` but each gets its own envelope. The historical "child folds into parent envelope" model is retired (per [`effects/01-effect.md`](features/effects/models/01-effect.md)).
- **`OriginCommandId` is engine-threaded.** The engine copies it from the just-executed command onto every evaluator follow-up; effects do not need to set it.
- **`OriginIntent` is recorded once, on the first envelope.** Subsequent envelopes in the chain share `OriginCommandId` but carry no `OriginIntent`.
- **`HistoryState` is framework-only.** Game authors never declare or write to it.

## Decisions

*n/a — no elevated ADR.* (The envelope shape and `HistoryState` slice are descriptive; they sit on top of the action pipeline ([`actions/01-command-pipeline.md`](features/actions/models/01-command-pipeline.md)) and the framework slice tier model ([`state/state.md`](features/state/state.md)). No options-weighing specific to this Model.)

## Examples

### Pile Counter envelope

```text
envelope publishes:
  - ZoneContentRemoved(A, card)
  - ZoneContentInserted(B, card, position)
  - VariableChanged(LastMove, ...)
  - Result: MoveCardResult(card, A, B)
  - OriginCommandId = newGuid
  - OriginIntent = MoveAttempt
```

### Fireball chain (three envelopes, shared `OriginCommandId`)

```text
HistoryEnvelope #1
  rootCommand: PlayFireballCommand
  originCommandId: G1
  originIntent: PlayFireballAttempt
  entries: [CostPaid, CardMovedHandToStack]

HistoryEnvelope #2
  rootCommand: DealDamageCommand
  originCommandId: G1
  originIntent: null
  entries: [DamageDealt, HealthChanged]

HistoryEnvelope #3
  rootCommand: MoveToDiscardCommand
  originCommandId: G1
  originIntent: null
  entries: [CardMovedStackToDiscard]
```

## Open follow-ups

- **Typed `HistoryEnvelope` C# record** — concrete record (fields: `envelopeId`, `originCommandId`, `actor`, `entries[]`, etc.). Pseudocode currently; pin when replay or multiplayer transport is designed.
- **Mandatory `HistoryEnvelope` metadata** — partially settled (`OriginCommandId`, `OriginIntent`, `Actor`, `SourceInstance`, `Result`, `Entries`); typed record pin tracked here.

---

*Below: provenance — n/a. This Model is purely descriptive (envelope shape follows from the command-pipeline contract); no separate options-weighing at this Model's scope.*

## Options considered

*n/a — descriptive Model.*

## Rationale

*n/a — see `## Contracts` above and the feature root's `## Invariants`.*

## Consequences

*n/a — see `## Contracts` above and the feature root's `## Invariants`.*
