---
type: adr
status: accepted
references: [features/actions/models/03-command-handler.md, features/actions/models/06-command-validator.md, features/actions/models/01-command-pipeline.md, features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, spikes/archive/fsm-architecture.md, decisions/0007-five-validation-layers.md, decisions/0009-engine-as-fsm.md]
tags: [#adr, #actions, #engine, #validation, #handler, #pipeline]
---

# ADR-0010 — Validator / handler split; `IHandlerContext` retired; framework intake validators

Date: 2026-05-11 (drafted from `Product/spikes/archive/fsm-architecture.md` D10–D11; sub-ADR B of the FSM-arch cascade following ADR-0009)
Context-docs: `Product/features/actions/models/03-command-handler.md`, new Model `Product/features/actions/models/06-command-validator.md`, `Product/features/engine/models/01-game-engine.md`, `Product/features/engine/models/03-game-rules-runtime.md`
Proposal: `Product/spikes/archive/fsm-architecture.md`

## Context

Two related shapes were tied together on the pre-ADR-0010 surface:

1. **Per-command legality was bundled into the handler.** `ICommandHandler<TCommand, TResult>` carried both `Validate(command, state, out reason)` and `Handle(command, ctx) → Task<TResult>`. The two responsibilities — "is this command legal right now?" and "apply this command's effects" — moved together. Reusing legality logic across commands (creature-tap rule, mana-cost rule, ownership rule) meant either copying it into every relevant handler's `Validate`, or projecting it into a separate helper class that handlers manually called. Layered validation (framework defaults + game-shipped legality + cost / anti-cheat) had no native channel; the "five validation layers" of [ADR-0007](Product/decisions/0007-five-validation-layers.md) lived informally as "stuff each layer does in its own code."

2. **Handlers ran inside `IHandlerContext`.** A handler-facing wrapper aggregated `Store`, `Actions` (the now-retired `IAvailableActions` snapshot writer — see [ADR-0009](Product/decisions/0009-engine-as-fsm.md)), `Resolver` (the `ICommandAwaiter` decision-await mechanism — superseded by the request system in the forthcoming ADR-0011), and `Modifiers` (the modifier-entry factory). The aggregator masked which services each handler actually needed and accumulated cross-cutting helpers. Two of its members (`Actions`, `Resolver`) are slated for removal by sibling ADRs; the remaining two (`Store`, `Modifiers`) are better expressed as DI dependencies — `Store` as a property-injected base-class member, `Modifiers` as constructor-injected on handlers that need it.

3. **The engine's intake gate was inline.** [ADR-0009](Product/decisions/0009-engine-as-fsm.md) added a two-layer intake gate inside `engine.Dispatch` — Layer 1 against `engine.Session`, Layer 2 against `MatchStateSlice.Current.Actions` — as a transitional shape. The cleaner expression of the same checks is registered framework validators (`SessionGate`, `MatchStateActionGate`) running through the validator pipeline alongside game-shipped ones. ADR-0009 explicitly flagged the conversion as ADR-0010's responsibility.

The 2026-05-10 open-questions walk on `Product/spikes/archive/fsm-architecture.md` settled the structural direction (D10–D11). This ADR ratifies it and cascades into the canonical Models.

## Options considered

- **A — Keep legality on the handler; introduce a `Diagnose(command)` helper that runs registered "validators" alongside `handler.Validate` for richer multi-reason output.** Rejected — the layered-validation pattern is the more general shape; `Diagnose` is a UI affordance derivable from the validator pipeline rather than a structural primitive. Building it on top of validators is straightforward; building validators on top of `Diagnose` inverts the dependency.
- **B — Split `Validate` from `Handle` into separate interfaces, but apply the split only to `GameAction`.** Rejected — the forthcoming ADR-0011 expands the `GameCommand` family (`Request<TResponse>`, `Response` siblings) and lands a single async `Dispatch` surface for all of them. Validators for actions, requests, responses, and signals all need the same machinery; making it action-only would force parallel infrastructure for the new siblings.
- **C — Split `ICommandValidator<TCommand>` and `ICommandHandler<TCommand, TResult>` into separate interfaces; introduce `GameCommandHandler<TCommand, TResult>` and `CommandValidator<TCommand>` abstract bases (each with property-injected `Store`); apply the split uniformly across all `GameCommand` subtypes; resolve all `ICommandValidator<TCommand>` for the runtime type at dispatch and run them in DI registration order, short-circuiting on first reject; retire `IHandlerContext` in favor of base-class property injection plus per-handler constructor injection for additional services; convert ADR-0009's two-layer inline intake gate to two registered framework validators (`SessionGate`, `MatchStateActionGate`).** **Chosen.**

## Decision

Four structural locks (mapping to D10–D11 of the FSM-arch spike plus the ADR-0009-flagged conversion).

**1. `Validate` splits from `Handle` into two separate interfaces.** `ICommandHandler<TCommand, TResult>` carries only `Handle`. `ICommandValidator<TCommand>` carries only `Validate`. DI resolves each independently; the pipeline composes them per dispatch.

```csharp
public interface ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    Task<TResult> Handle(TCommand command);
}

public interface ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    bool Validate(TCommand command, out string? reason);
}
```

**Validators are independent.** Each receives only the command. State (when needed) comes via DI on the abstract base, not a shared per-dispatch context bag. Two validators never share writable scratchpad; they each answer their own question against the same store.

**Validator policy.**

- **Short-circuit on first reject.** First validator to reject wins; subsequent ones do not run. One reason is surfaced to the caller. A "list every reason" surface (`Diagnose(command)`) is a separate UI affordance — runs all validators non-short-circuit, returns a list — built on top of the same registry. Not part of the dispatch path.
- **Registration order = run order.** DI registration order determines the run order. Reorder by changing registration. Convention: framework defaults register first; games append after. Documenting whether the convention is enforced by the framework is `oq-validator-defaults`.
- **Optional registration.** Commands with no validator registered skip the check (no-op). Validation is opt-in per command type.
- **Uniform across all `GameCommand` subtypes.** The same `ICommandValidator<TCommand>` machinery applies to *any* `GameCommand` subtype — `GameAction`, `CompositeAction`, `GameSignal`, and the forthcoming `Request<TResponse>` / `Response` siblings (ADR-0011). One pipeline; one registry.

**2. `GameCommandHandler<TCommand, TResult>` and `CommandValidator<TCommand>` abstract bases with property-injected `Store`.** Most handlers and validators just override one method; they get `Store` access via a `[Inject]` property on the base class. Specific services (modifier factory, game-specific subsystems) come in via the subclass's constructor.

```csharp
public abstract class GameCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract Task<TResult> Handle(TCommand command);
}

public abstract class CommandValidator<TCommand>
    : ICommandValidator<TCommand>
    where TCommand : GameCommand
{
    [Inject] protected Store Store { get; private set; } = null!;
    public abstract bool Validate(TCommand command, out string? reason);
}

// Combined base for the common case: one class that both validates and handles its command.
public abstract class ValidatedCommandHandler<TCommand, TResult>
    : GameCommandHandler<TCommand, TResult>, ICommandValidator<TCommand>
    where TCommand : GameCommand
    where TResult  : CommandResult
{
    public abstract bool Validate(TCommand command, out string? reason);
}
```

**Property injection is "hidden" relative to constructor injection** — acknowledged tradeoff. Worth it because the floor case (override one method; that's it) stays trivially small, and framework-level services that grow over time land on the base class without touching subclasses. Handlers / validators that need additional services declare them via the constructor (explicit), as before.

**`IHandlerContext` is removed.** The wrapper aggregator is gone. Of its four pre-ADR-0010 members:

- `Store` — moves to property injection on the handler base.
- `Modifiers` (the modifier-entry factory) — constructor injection on the handlers that need it.
- `Actions` (the `IAvailableActions` snapshot writer) — already retired by ADR-0009.
- `Resolver` (the `ICommandAwaiter` decision-await mechanism) — retired by ADR-0011 in favor of the request system; handler bodies become atomic from the player-input POV.

**3. Single `Dispatch` shape; validators run before handler resolution.** The pipeline resolves the registered validators for the command's runtime type, runs them in registration order, short-circuits on first reject, and only then resolves the handler.

```text
Dispatch(command):
  resolve all ICommandValidator<TCommand> registered for typeof(command)
  for each in registration order:
    if !validator.Validate(command, out reason):
      return Reject(reason)                          // short-circuit
  route by kind:
    GameAction      → resolve handler; await Handle; envelope; before/after-fold around
    CompositeAction → inline expansion in parent scope
    GameSignal      → write to history
    Request<T>      → mutate RequestsSlice           (ADR-0011)
    Response        → mutate AnswersSlice + clear RequestsSlice (ADR-0011)
```

**Handlers are atomic from the player-input POV.** A handler returns its `CommandResult` synchronously (or with `Task.FromResult`) — no `await` on player decisions inside `Handle`. Player decisions (target picks, choose-one prompts) flow through the forthcoming request system (ADR-0011); from the handler's perspective they don't exist. The previously-async `Handle` signature is preserved (effects can still `await scope.Dispatch(...)` orchestration), but handler bodies stop containing player-input awaits.

**4. ADR-0009's inline intake gate is converted to two registered framework validators.** Layer 1 becomes `SessionGate : CommandValidator<GameCommand>` — runs against every command, reads `engine.Session`, rejects when the engine is not accepting commands. Layer 2 becomes `MatchStateActionGate : CommandValidator<GameCommand>` — runs against every command, reads `MatchStateSlice.Current.Actions`, rejects when the command's type isn't in the active state's `Actions` list. Both register before any game-shipped validator (framework-default convention; see Notes).

```csharp
// Framework-default. Layer 1: session-level gate.
public sealed class SessionGate : CommandValidator<GameCommand>
{
    private readonly IEngineSession session;   // narrow capability — reads engine.Session
    public SessionGate(IEngineSession session) { this.session = session; }

    public override bool Validate(GameCommand command, out string? reason)
    {
        if (session.Current is EndedSession) {
            reason = "MatchEnded";
            return false;
        }
        if (session.Current is NotStartedSession
            && command is not StartMatchCommand
            && command is not EndMatchCommand) {
            reason = "MatchNotStarted";
            return false;
        }
        reason = null;
        return true;
    }
}

// Framework-default. Layer 2: match-state-actions gate.
public sealed class MatchStateActionGate : CommandValidator<GameCommand>
{
    public override bool Validate(GameCommand command, out string? reason)
    {
        if (command is StartMatchCommand or EndMatchCommand or TransitionStateCommand) {
            reason = null;
            return true;   // lifecycle commands bypass
        }
        var current = Store.Get<MatchStateSlice>().Current;
        if (current.Actions.Count > 0 && !current.Actions.Contains(command.GetType())) {
            reason = $"NotInMatchStateActions:{current.GetType().Name}";
            return false;
        }
        reason = null;
        return true;
    }
}
```

The narrow `IEngineSession` capability (read-only view of `engine.Session`) is the same surface ADR-0009 exposed as `engine.Session` plus the `SessionChanged` event; only the validator needs the read side.

`engine.Dispatch` becomes shape-thin: `pipeline.Validate(command)` resolves and runs the registered chain (including the two framework defaults); engine-internal `TransitionStateCommand`s still bypass the chain (framework-internal, enter the queue directly).

## Consequences

- **Enables** layered validation natively. ADR-0007's five validation layers each compose as one or more `ICommandValidator<TCommand>` registrations — framework defaults at one end of the chain, game-shipped legality / cost / anti-cheat / housekeeping at the other. Order, enable/disable, and replacement are all DI-registration operations.
- **Enables** reusable validators. A "creature must be untapped to tap" rule lives once as `CommandValidator<TapCreatureCommand>` (or as a cross-cutting validator targeting any tap-shaped command); every command type that involves the rule shares the implementation. No more copy-pasting legality across handlers.
- **Enables** uniform validation across the upcoming `GameCommand` family expansion. Request validators (`TargetResponseValidator` — "is the chosen id in the valid set?"), signal validators (rare, but available), and composite-action validators all use the same registry. ADR-0011's `Request<TResponse>` / `Response` siblings inherit the pipeline at no additional cost.
- **Enables** a diagnostic surface (`Diagnose(command) → IReadOnlyList<string>`) that runs all validators non-short-circuit. UI can use it to explain "why can't I play this?" with one entry per failing layer. Not on the dispatch path; cosmetic affordance built on top.
- **Enables** retiring the inline intake-gate scaffolding. `engine.Dispatch` shrinks to: bypass-test for framework-internal `TransitionStateCommand`, then `pipeline.Validate`, then enqueue / drain. The gate logic is data in DI registrations, not control flow in `engine.Dispatch`.
- **Constrains** every handler / validator to land in the new typed-base shape. Anyone holding the old `ICommandHandler<TCommand, TResult>.Validate(...)` member must migrate to `ICommandValidator<TCommand>`. No backward-compatible shim — straight cutover. (Acceptable: the framework is pre-v1 and no game ships against the old surface.)
- **Constrains** validators to be independent. Two validators cannot share a per-dispatch scratchpad. State sharing happens through the `Store` (read), the command itself, or — when truly shared logic is needed — by composing into a helper that the validators call. The "context bag" pattern is structurally absent.
- **Constrains** handler bodies to stay atomic from the player-input POV. No `await ctx.Resolver.Wait<TCommand>()` inside `Handle` — the resolver is gone. Effects orchestrate player decisions via `await scope.Dispatch(request)` once ADR-0011 lands; until then, the few decision-handler patterns in the existing Models (e.g., `RequestTargetHandler` in `effects/04-command-awaiter.md`) carry a transitional rework-pending banner.
- **Retires** `IHandlerContext` and the canonical Model that documented it (`Product/features/effects/models/03-handler-context.md`) — archived to `Product/features/effects/archive/03-handler-context.md`. References to `ctx.Store` / `ctx.Store.Execute(payload)` in handler bodies become `Store.Execute(payload)` (the base-class property).
- **Retires** the inline two-layer intake gate in `engine.Dispatch`. The control-flow disappears; the gates become DI registrations (`SessionGate`, `MatchStateActionGate`). Layer 1 / Layer 2 vocabulary survives in the validator names.
- **Reverts / replaces** the bundling of `Validate` into `ICommandHandler<TCommand, TResult>`. The single combined interface no longer exists at framework level; `ValidatedCommandHandler<TCommand, TResult>` is the explicit combined base for the common case where one class authors both.
- **Refines** [ADR-0007](Product/decisions/0007-five-validation-layers.md): the "five validation layers" principle is unchanged in spirit (multiple owners; one authoritative answer; advisory vs structural roles). The "handler.Validate is authoritative" wording becomes "the registered validator chain is authoritative" — typically the *last* game-shipped validator in the chain owns the per-command legality answer, while framework validators handle session / match-state gating earlier. Both engine-flow-gate and match-state-action-gate layers are now framework validators rather than inline engine logic. ADR-0007 carries an amendment-2026-05-11 note pointing here.
- **Defers** the framework's default validator set crystallization (`oq-validator-defaults`): `SessionGate` and `MatchStateActionGate` are the two canonical defaults. Whether registration order is enforced (vs by convention) and whether a third framework-default validator (e.g., a "command isn't disposed / engine not disposed" guard) joins them is left open.
- **Defers** the `Diagnose(command)` surface shape. Whether it lives on `ICommandPipeline` or as a separate `ICommandDiagnoser` service, and whether each validator can opt out of `Diagnose` (e.g., a `[Skip(Diagnose)]` attribute for performance-sensitive checks), waits for the first UI consumer.

## Notes

- **Sub-ADR cascade position.** This is sub-ADR B of the FSM-arch cascade (independent of ADR-0009; ADR-0011 — effect orchestration + dispatch + requests — and ADR-0012 — presentation per-state ViewModels — round it out). The structural separation of validator and handler is a precondition for ADR-0011's `Request<TResponse>` / `Response` siblings — the request-system validators (`TargetResponseValidator` etc.) rely on this surface.
- **Spike provenance.** `Product/spikes/archive/fsm-architecture.md` D10–D11 are the historical source. ADR-0011 and ADR-0012 have since landed; the spike was archived in the post-cascade consolidation PR (2026-05-11) as `superseded` referencing 0009 / 0010 / 0011 / 0012.
- **`IHandlerContext` glossary entry.** Already listed in the GLOSSARY's "Retired terms" table (per the 2026-05-10 walk). This ADR's cascade removes the Model that documented it; the glossary entry's note now points here.
- **Transitional handlers in existing Models.** A handful of Models include handler example code still using the `IHandlerContext` parameter shape — most notably `engine/07-match-state.md`'s `TransitionStateHandler` and `effects/04-command-awaiter.md`'s `RequestTargetHandler`. The transition-state handler's *behavior* is unchanged; only the registration shape moves to `GameCommandHandler<,>` base. The request-target handler is wholly superseded by the request-system pattern in ADR-0011 and carries a rework-pending banner.
- **Validator registration ergonomics.** The framework ships `SessionGate` and `MatchStateActionGate` registered first; games append. Whether to expose a `[ValidatorOrder]` attribute or `services.AddValidatorBefore<...>` extension method for finer control is deferred until a real game stresses ordering.
- **`Diagnose` surface.** Tracked informally; lands when a UI consumer needs multi-reason rejection feedback. Until then, the `bool Dispatch(GameAction, out string?)` try-get pattern returns a single reason, which is sufficient for toast-style UX.
- **No change to before-fold cancel.** The trigger pipeline's `OnBefore<T>` cancel/replace path (per `features/triggers/models/02-trigger-pipeline.md`) is unchanged — that's a *mid-execution* rejection, not an intake gate, and runs after validators succeed and the handler enters the trigger fold. The five rejection paths table in `features/engine/models/03-game-rules-runtime.md` accounts for both.
