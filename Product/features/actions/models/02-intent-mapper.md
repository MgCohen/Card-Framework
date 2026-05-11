---
type: model
status: settled
references: [features/actions/models/01-command-pipeline.md, features/actions/models/05-available-actions-snapshot.md, features/triggers/models/01-trigger.md, decisions/0007-five-validation-layers.md]
tags: [#actions, #model, #intent]
---

# Intent Mapper — model

## What it is

`IIntentMapper.Map(template, choice) → GameCommand` is the boundary that turns external player intent into a typed, dispatchable command. `PlayerIntent` enters from outside the rules layer (UI, AI, network, scripted, replay); `IntentGateway` orchestrates the boundary; the mapper does the typed switch from `ActionTemplate` to `GameCommand`.

```text
PlayerIntent (UI / AI / network / scripted / replay)
  → IntentGateway.Submit(intent, template, choice)
     → IIntentMapper.Map(template, choice) → GameCommand
     → engine.Dispatch(command)
```

## Contracts

### `PlayerIntent`

A structured intent submitted from outside the rules layer. Carries what the actor attempted, **not** the final state mutation. Sources:

- Player input (drag, click, target).
- AI choice.
- Network message.
- Scripted enemy behavior.
- Triggered system.
- Tutorial automation.
- Replay playback.

Examples: drag instance A from zone X to zone Y; play card A; activate ability on instance A; choose option 3; pass priority; buy item from shop.

### `PlayerChoice`

The data the player committed to: target list, parameter values, mode selections. Bound by `IIntentMapper` against an `ActionTemplate` (defined in [`05-available-actions-snapshot.md`](features/actions/models/05-available-actions-snapshot.md)) to produce a `GameCommand`.

### `IntentGateway`

The thin entry layer that translates `PlayerIntent` → dispatched `GameCommand`. **Game-supplied** (each game ships its own); the framework provides the contract through `IIntentMapper`.

```csharp
public sealed class IntentGateway
{
    private readonly GameEngine engine;
    private readonly IIntentMapper mapper;
    private readonly IStateScope state;

    public IntentGateway(GameEngine engine, IIntentMapper mapper, IStateScope state) { ... }

    public IntentResult Submit(PlayerIntent intent, ActionTemplate template, PlayerChoice choice)
    {
        var snapshot = state.Get<AvailableActionsState>().Snapshot;
        if (!snapshot.Templates.Contains(template))
            return IntentResult.Rejected("not in snapshot");

        var command = mapper.Map(template, choice);
        var dispatched = engine.Dispatch(command);
        return dispatched.Accepted ? IntentResult.Ok : IntentResult.Rejected(dispatched.Reason);
    }
}
```

### `IIntentMapper`

```csharp
public interface IIntentMapper
{
    GameCommand Map(ActionTemplate template, PlayerChoice choice);
}
```

The mapper switches on the typed `ActionTemplate` subtype to produce a well-formed command. **Each game ships its own implementation.** **Decision-unaware** — produces the same command shape whether or not a decision is open. The engine's pre-pipeline awaiter check (per [`effects/04-command-awaiter.md`](features/effects/models/04-command-awaiter.md)) intercepts decision-matching commands before the normal pipeline runs.

```csharp
public GameCommand Map(ActionTemplate template, PlayerChoice choice) => template switch
{
    MoveCardTemplate     m => new MoveCardCommand(m.Card, state.LocateCard(m.Card), (Ref<Zone>)choice.Targets[0]),
    ChooseTargetTemplate t => new SelectTargetCommand(choice.Targets[0]),
    _ => throw new InvalidOperationException($"unknown template: {template}")
};
```

`IIntentMapper.Map` is one of the five validation layers ([ADR-0007](Product/decisions/0007-five-validation-layers.md)) — it gates well-formedness, not legality. Authoritative legality is the registered validator chain at dispatch (per [ADR-0010](Product/decisions/0010-validator-handler-split.md); see [`06-command-validator.md`](features/actions/models/06-command-validator.md)).

> **Rework pending.** This Model is superseded by [ADR-0012 — *forthcoming*] — the global `IIntentMapper` retires in favor of per-state `MatchStateViewModel<TState>.Map(intent)`. Content here is preserved as the pre-ADR-0012 shape; for the post-cascade picture see the ADR when it lands.

## Invariants

- **UI creates intents, not state changes.** `PlayerIntent` is the boundary; everything beyond is framework / game logic.
- **Mapper is decision-unaware.** No snapshot scope inspection. The mapper produces `SelectTargetCommand` whether or not a decision is open — it does not know.
- **Each game ships its own mapper and gateway.** The framework provides only the `IIntentMapper` contract.
- **Mapper output is well-formed but not yet legal.** Authoritative legality is the registered validator chain at dispatch (per [`06-command-validator.md`](features/actions/models/06-command-validator.md) and [ADR-0010](Product/decisions/0010-validator-handler-split.md)).

## Decisions

Linked: [`Product/decisions/0007-five-validation-layers.md`](Product/decisions/0007-five-validation-layers.md) — `IIntentMapper.Map` is one of the five layers.

## Examples

For an end-to-end worked mapper (typed templates, switch on template type, `IStateScope` for state-driven mapping like locating the card's source pile), see the `PileCounterIntentMapper` in [`engine/04-available-actions.md` §IntentMapper becomes typed](features/engine/models/04-available-actions.md). The mapper there exercises both the typed-template switch and the decision-unaware property — it never queries "is a decision open?", just maps templates to commands.

## Open follow-ups

- **`PlayerChoice` shape under typed templates** — may collapse to positional `IReadOnlyList<EntityId>` + `IReadOnlyDictionary<string, object>` parameters now that templates carry typed slot data. Pin when `IIntentMapper` is implemented.
- **`bl-interaction-feature`** — `interaction/` feature for the gesture-to-intent layer (the layer between presentation gestures and `PlayerIntent`). All intent vocabulary depends on it.
- **Rollback / rejection signaling to presentation** — how rejected dispatches surface to UI (toast? state subscription? envelope entry?). Currently game-supplied via the `IntentGateway`'s return.

---

*Below: provenance — feature-internal decisions folded on 2026-05-08; extracted from `01-command-pipeline.md` on 2026-05-09 along with the Q4 and Q5 decision history.*

## Options considered

### Q1 — `PlayerIntent → IIntentMapper → GameCommand` (formerly ADR-0006 in actions/ — folded)

UI gestures (drag, click, target) produce something that is *not yet* a `GameCommand`. We needed a layer that takes UI intent + the chosen template and produces a well-formed command, without UI knowing command shapes.

- **A — UI constructs `GameCommand` directly.** Rejected — UI ends up carrying command-shape knowledge for every game.
- **B — Reflection-driven slot filling.** Rejected — fragile and runtime.
- **C — `IIntentMapper.Map(template, choice) → GameCommand`, switch on typed template subtype, returns the well-formed command.** **Chosen.** Each game ships its own mapper; the framework provides the contract.

(Note: this was originally numbered ADR-0006 in `features/actions/decisions/` — a per-feature counter. The number is unrelated to the project-wide `Product/decisions/0006-two-pipeline-architecture.md`.)

### Q2 — Intent vocabulary rename (formerly ADR-0008 — folded)

Earlier docs used `Request` / `OriginRequest` for what the player asks for, and `Command` for what the engine executes. "Request" overloaded against HTTP-style semantics and didn't read clearly when validation was distributed.

- **A — Keep `Request` everywhere.** Rejected — term conflicting with the on-demand-validation framing.
- **B — Use `Proposal` / `Action` / `Command` as a three-tier vocabulary.** Rejected — added a layer for no semantic gain.
- **C — `PlayerIntent` (UI), `Action` / `ActionTemplate` (snapshot), `GameCommand` (executed).** **Chosen.** Source/origin fields renamed `OriginIntent`. Load-bearing rename that cascaded across earlier docs.

## Rationale

Q1 keeps UI decoupled from command shapes — UI knows templates and choices; the mapper bridges to commands. Q2's `Intent → Action → Command` vocabulary makes the pipeline readable: an intent becomes a typed action and dispatches as a command.

## Consequences

- **Enables:** UI that only knows templates/choices; consistent reading of the pipeline; decision-unaware intake.
- **Constrains:** every command needs a paired template; any code/doc still using `Request` is out of date.
- **Cascade:** consumed by `presentation/01-presentation-loop.md` (`AffordanceMap` consumes typed templates); `effects/04-command-awaiter.md` (engine pre-pipeline awaiter check intercepts decision-matching commands).
