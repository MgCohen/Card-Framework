---
type: model
status: draft
references: [features/commands/commands.md, features/triggers/models/02-trigger-fold.md, features/commands/models/01-command-handler.md, features/commands/models/03-history-envelope.md]
tags: [#commands, #pipeline]
---

# Command Pipeline

## What it is

The framework's per-command executor: a five-phase procedure that runs one `GameCommand` through validation, before-fold triggers, the registered handler, after-fold triggers, and envelope emission. The pipeline is generic; what fires inside (which handler, which triggers) is game-supplied via DI.

```
Pipeline.Execute(command):
    envelope := NewEnvelope(command)

    triggers.OnBefore<T>(command, scope) → command' | cancelled | replaced
    if cancelled:
        envelope.Result := Rejected
        return envelope

    handler := registry.Resolve<TCommand, TResult>()
    result  := handler.Execute(command', ctx)
    envelope.Result := result

    triggers.OnAfter<T>(command', result, scope)

    return envelope
```

## Shape

`ICommandPipeline` is the framework's executor — one DI-registered service per `GameEngine` instance. It does not own the queue or the drain loop (those live in `engine/`); it processes one command per `Execute` call.

```csharp
public interface ICommandPipeline
{
    bool             Validate (GameCommand command, IStateScope state, out string? reason);
    HistoryEnvelope  Execute  (GameCommand command);
}
```

The handler registry is a separate DI-resolved service the pipeline consults to find `ICommandHandler<TCommand, TResult>` for the command's runtime type. The trigger pipeline is owned by `triggers/`; the command pipeline calls into it for the before/after folds.

## Contracts

**`Validate(command, state, out reason) → bool`**
- Resolves the handler from the registry by command type.
- Delegates to `handler.Validate`.
- Returns `false` with reason if handler not found *or* handler rejects.
- Side-effect-free.

**`Execute(command) → HistoryEnvelope`**
- Five-phase procedure (see `## What it is`).
- Always returns an envelope, including for cancelled / rejected commands (with `Result.Status = Cancelled / Rejected`).
- Phases are sequential and not interleavable. Effects calling `scope.Dispatch` reentrantly produce siblings, not nested phases.

**Forbidden:**
- Pipeline does not invoke mutators directly. (Only handlers do.)
- Pipeline does not consult game logic for "what runs next." (That's the evaluator, owned by `engine/`.)
- Pipeline does not await. (Decision-handlers handle awaiting via `IHandlerContext`, owned by `effects/`.)

## Invariants

- Phases run in the documented order; no skipping except cancellation.
- A cancelled command skips handler execution but still emits an envelope with cancellation entries.
- The `command'` after the before-fold may differ from the input command in field values *or* concrete type (replacement). Handler resolution uses the post-fold type.
- The trigger pipeline is called for both before and after, with the pipeline phase as discriminator.

## Decisions

*(none — feature-internal decisions sit below the divider)*

## Examples

A `DealDamageCommand(target=Goblin, amount=5)` traced through:

```
1. Validate
   → registry.Resolve<DealDamage, DamageResult> → DealDamageHandler
   → handler.Validate(cmd, state) → true
   → returns true

2. Execute
   2a. envelope := NewEnvelope(cmd)
       envelope.OriginCommandId := newId()
       envelope.Actor := cmd.Source

   2b. OnBefore<DealDamageCommand> fires:
       Trigger 1: "Spell Shield absorbs first damage" → scope.Cancel()
       (later triggers skipped on cancellation)

   2c. cancelled → handler not invoked
       envelope.Result := Cancelled("spell shield")
       envelope.Entries += [CommandCancelled(reason: "spell shield")]

   2d. OnAfter<DealDamageCommand> still fires:
       Trigger: "After cancelled damage attempt, log it" → no-op here

   2e. return envelope

3. Pipeline returns to engine; engine puts envelope on the bus
```

A non-cancelled trace would replace 2c with `handler.Execute(...)` → `DamageResult(shieldHit=2, hpHit=3, killed=false)`, and the envelope would carry `ReduceShield`, `ReduceHealth` entries.

## Open follow-ups

- **Replacement re-validation.** If `OnBefore` *replaces* a `DealDamage` with a `Heal`, do we re-run `Validate` against the replacement? Current position: yes (handler must accept it). Captured as `oq-pipeline-replacement-revalidation`.

---

*Below: provenance.*

## Options considered

- **A** — Pipeline owns the queue. *Rejected:* couples the per-command procedure to the engine loop; can't unit-test the pipeline in isolation.
- **B** — Pipeline calls handler directly with no fold step. *Rejected:* no place for replacement / prevention. Triggers would have to be invoked outside the pipeline, fragmenting the algorithm.
- **C — Pipeline owns the per-command procedure; engine owns the loop; triggers fold inside before/after phases.** *Chosen.*

## Rationale

Single-responsibility cleavage: pipeline = one command, engine = many commands. Triggers folding inside the pipeline (rather than alongside it) keeps the modify-cancel-replace semantics at one site and gives the handler a single point of entry.

## Consequences

- **Enables:** unit testing the pipeline against any registered handlers without an engine; effect-side `scope.Dispatch` becomes a recursive pipeline call sharing `OriginCommandId`.
- **Constrains:** triggers can't reorder relative to the handler — the before/after split is structural; per-trigger ordering policy lives in `triggers/` (replacement-prevention rules).
- **Reverts/replaces:** the deprecated `CommandPreHook` and `CommandReaction` placeholders from earlier drafts.
