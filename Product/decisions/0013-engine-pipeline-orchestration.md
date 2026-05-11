---
type: adr
status: accepted
references: [features/engine/models/01-game-engine.md, features/engine/models/03-game-rules-runtime.md, features/actions/models/01-command-pipeline.md, features/actions/models/06-command-validator.md, features/triggers/triggers.md, features/triggers/models/01-trigger.md, features/triggers/models/02-trigger-pipeline.md, features/effects/models/01-effect.md, features/effects/models/02-effect-scope.md, spikes/runtime-walkthrough.md, decisions/0006-two-pipeline-architecture.md, decisions/0010-validator-handler-split.md, decisions/0011-effect-orchestration-dispatch-requests.md]
tags: [#adr, #engine, #orchestration, #actions, #triggers, #effects, #dispatch]
---

# ADR-0013 — Engine-driven pipeline orchestration; single `Dispatch` surface; per-type routing; `runtime.FilterTriggers` retires

Date: 2026-05-11 (drafted from the runtime-walkthrough spike Q6 walk; refines the orchestration parts of ADR-0006, ADR-0010, ADR-0011)
Context-docs: `Product/spikes/runtime-walkthrough.md`, `Product/features/engine/models/01-game-engine.md`, `Product/features/actions/models/01-command-pipeline.md`, `Product/features/triggers/triggers.md`, `Product/features/effects/models/02-effect-scope.md`
Source: `Product/spikes/runtime-walkthrough.md` Q6 design walk (2026-05-11)

## Context

After ADR-0006 (two-pipeline architecture), ADR-0010 (validator/handler split), and ADR-0011 (effect orchestration with unified `Dispatch`), three layering tensions remained latent at the orchestration level:

1. **The action pipeline held `ITriggerPipeline`.** Per the pre-cascade design ([`triggers/triggers.md`](features/triggers/triggers.md) §Pipeline integration: *"the action pipeline holds `ITriggerPipeline`"*), the actions feature consumed the triggers feature to run before/after fold inside its drain. Mechanically this works through an interface, but architecturally the *actions* concept is supposed to be "commands, validators, handlers, envelope shape" — knowing about triggers stretches its surface beyond single-concern.
2. **`runtime.FilterTriggers` was an upstream filter call from the action pipeline into the runtime.** It existed to let game-side eligibility ("am I on the battlefield?") gate which triggers fire. The mechanism worked but spread trigger eligibility logic across two layers — the trigger itself (its own `Validate`) and the runtime (a separate `FilterTriggers` pass). Two filter call-sites for one concept.
3. **`scope.Emit(signal)` and `scope.Execute(composite)` bypassed `engine.Dispatch`.** Per [`triggers/triggers.md:121`](features/triggers/triggers.md): *"`scope.Emit` and `scope.Execute` are the scope-level equivalents of `engine.Dispatch` for non-queued types."* Three scope-level entry points for the five `GameCommand` siblings. The "single unified `Dispatch`" goal of ADR-0011 D14 didn't carry all the way through.

Pushed together, these mean: the orchestration of "what fires for which command type" lives in multiple places — the action pipeline owns drain + trigger fold + handler dispatch; the runtime owns trigger filtering; the scope owns three different emit pathways for the five sibling family. Reasoning about a single command's journey through the system requires reading code in three features at once.

The runtime-walkthrough spike's Q6 design walk surfaced a cleaner shape: **the engine is the orchestrator; each pipeline feature is single-concern; all five siblings flow through one Dispatch surface; the engine pattern-matches command type and routes to the right pipeline at the right step.** This ADR ratifies that shape.

## Options considered

- **A — Keep the pre-cascade design.** Actions holds `ITriggerPipeline`; runtime owns `FilterTriggers`; scope has three entry points. Rejected — the layering tensions surface during code review and authoring; the "single Dispatch" promise isn't kept; trigger eligibility lives in two places.
- **B — Move the trigger fold into the engine but keep `FilterTriggers` + scope's three entry points.** Half-measure. Removes the actions ↔ triggers cycle but doesn't simplify the surface.
- **C — Engine orchestrates everything; pipelines are focused services; one `scope.Dispatch(GameCommand)`; `runtime.FilterTriggers` retires; trigger eligibility lives in `Trigger<T>.Validate`.** **Chosen.**

## Decision

Six structural locks. Each lock cascades into the relevant Model and the docs cross-referencing it.

### 1. Engine is the pipeline orchestrator

The engine's drain loop owns the per-command-type routing decision. It holds references to the action pipeline (validators + handlers), the trigger pipeline (`ITriggerPipeline`), and the effect pipeline (drain on `PendingEffectsSlice`). It pattern-matches command type and invokes the right subsystem at the right step:

```csharp
// Inside engine drain loop.
private void Execute(GameCommand cmd) {
    switch (cmd) {
        case GameAction action:
            var routed = triggerPipeline.RunBefore(action, scope);   // may modify/cancel/replace
            if (routed.Cancelled) return;
            var result = actionPipeline.Execute(routed.Command);     // handler runs
            triggerPipeline.RunAfter(routed.Command, result, scope);
            lastResult = result;
            break;

        case GameSignal signal:
            // No validator chain. No handler. Folds only.
            triggerPipeline.RunBefore(signal, scope);
            triggerPipeline.RunAfter(signal, NoResult.Instance, scope);
            break;

        case Request<TResponse> req:
            actionPipeline.Execute(req);   // handler writes RequestsSlice.Open
            break;

        case Response resp:
            actionPipeline.Execute(resp);  // handler writes AnswersSlice + clears RequestsSlice
            break;

        case CompositeAction:
            throw new InvalidOperationException(
                "CompositeAction expands inline within parent scope; should never reach Dispatch");
    }
}
```

The concrete factoring (whether this is one `switch`, several methods, or a polymorphic `ICommandExecutor` strategy) is a code-time decision; the contract is that engine — not actions, not the runtime — owns the routing.

### 2. Pipelines are single-concern

| Feature | Owns | Doesn't own |
|---|---|---|
| `actions` | `GameCommand` family, `ICommandValidator<T>` chain, `GameCommandHandler<T,R>`, `HistoryEnvelope` shape | Triggers, effects, drain orchestration |
| `triggers` | `Trigger<T>` (`Validate` + `Execute`), `ITriggerSystem`, `ITriggerPipeline`, in-memory registry, fold mechanic | Knowledge of specific commands beyond the type parameter; effects |
| `effects` | `IEffect.Run`, `IEffectScope` (single `Dispatch`), `PendingEffectsSlice`, request system | Triggers, validators, drain orchestration |
| `engine` | Pipeline orchestration (per-type routing); FSM evaluation; session lifecycle | — |

No feature holds a reference to another pipeline feature's concrete types. Engine consumes the three pipelines as interfaces.

### 3. Single `Dispatch` surface — `scope.Emit` and `scope.Execute` retire

`IEffectScope` exposes one method:

```csharp
public interface IEffectScope {
    Task<TResult> Dispatch<TResult>(GameCommand cmd) where TResult : CommandResult;
}
```

`scope.Emit(signal)` and `scope.Execute(composite)` are removed. Effect bodies, handler bodies, trigger reactions — all dispatch through the same `scope.Dispatch(GameCommand)`. Engine routes by type.

`engine.Dispatch(GameCommand)` (external entry) and `scope.Dispatch(GameCommand)` (inside-handler / inside-effect entry) take the same shape.

### 4. `runtime.FilterTriggers` retires; `Trigger<T>.Validate` is THE eligibility surface

`GameRulesRuntime` no longer carries `FilterTriggers`. Trigger eligibility moves fully into the trigger itself:

```csharp
public abstract record Trigger<T> where T : GameCommand {
    public abstract bool         Validate(T cmd, IEffectScope scope);
    public abstract Task<...>    Execute (T cmd, IEffectScope scope);
}
```

`TriggerSystem.RunBefore(cmd, scope)` iterates its in-memory registry for type `T`, calls `Trigger<T>.Validate` on each candidate, collects the passing ones, and folds them. Same for `RunAfter`. No central filter; no two-step process.

Game-wide eligibility policies (e.g., "no triggers fire during a particular phase") express through trigger-author convention or a future game-wide predicate registry. The v1 shape is per-trigger `Validate` only.

### 5. One trigger registration mechanism — same API for install-time and runtime

There is no "static vs dynamic" trigger category. All triggers register via `ITriggerSystem.OnBefore<T>(...)` / `.OnAfter<T>(...)`. The framework expects:

- **Game install** registers framework-permanent triggers up front (most of them).
- **`AbilityDefinition.Register(triggers, self)`** registers per-instance triggers when an instance enters play; `Unregister` when it leaves.

Same surface; the only variable is *when* `Register` is called. No two-track design.

### 6. Trigger registry is in-memory for v1

`TriggerSystem` holds its registry in process memory (e.g., `Dictionary<Type, List<Trigger>>` or similar). Determinism for replay is preserved because every `Register`/`Unregister` is driven from deterministic places (game install, `AbilityDefinition` lifecycle hooks tied to instance entry/leave events that themselves run through the deterministic command pipeline). The sequence of registrations is reconstructible from history.

Slice-resident registry deferred until the broader "slice-linked services" pattern formalizes.

### Per-type routing table

| Sibling | Queued? | Validator chain? | Trigger fold? | Handler? | History envelope? |
|---|---|---|---|---|---|
| `GameAction` | Yes (FIFO drain) | Yes | Before + After | Yes | Yes |
| `GameSignal` | Yes | No | Before + After | No | Yes (fact-only) |
| `Request<TResponse>` | Yes | Yes (`SessionGate`; chain TBD on inheritance — see Deferred) | No (deferred — see [`runtime-walkthrough`](spikes/runtime-walkthrough.md) Q8) | Yes (writes `RequestsSlice.Open`) | Yes |
| `Response` | Yes | Yes (same as Request) | No (same) | Yes (writes `AnswersSlice` + clears `RequestsSlice`) | Yes |
| `CompositeAction` | No (inline within parent scope) | No | No (sub-items fold individually) | No | No own envelope |

## Deferred for implementation time

Three sub-questions don't need to settle in this ADR; tracked for code time.

- **Request / Response inheritance from `GameAction`.** If `Request<T> : GameAction` and `Response : GameAction`, validators apply by default (uniform). If they're sibling-but-not-action, separate validation wiring. Decide when concrete handlers are first authored.
- **Game-wide trigger eligibility predicates beyond `Trigger<T>.Validate`.** v1: per-trigger validation only. If scale or recurring authoring friction surfaces, introduce a central predicate registry. Track as `oq-trigger-eligibility-policy`.
- **Trigger registry as a Store slice.** Migration when the slice-linked-services pattern formalizes. Track as `oq-trigger-registry-slice`.

## Consequences

### What this supersedes / refines

| Source | What changes |
|---|---|
| **ADR-0006 (two-pipeline)** | The "framework command pipeline + game effect pipeline" framing stands. Refined: the *orchestration* of those pipelines lives in `engine`, not in `actions`. Pipeline features become focused services consumed by the engine. |
| **ADR-0010 (validator/handler split)** | Validator chain authoritative. Refined: chain runs for `GameAction`, `Request`, `Response`; `GameSignal` and `CompositeAction` bypass. Framework intake validators (`SessionGate`, `MatchStateActionGate`) still register first. |
| **ADR-0011 (effect orchestration; unified `Dispatch`)** | Single `Dispatch` extends to scope-level: `scope.Emit` + `scope.Execute` retire; `scope.Dispatch(GameCommand)` is the sole inside-handler / inside-effect entry. Five-sibling family stays. |

### What's net-new

- `runtime.FilterTriggers` retired. `Trigger<T>.Validate` carries the eligibility load.
- `scope.Emit` + `scope.Execute` retired. One `scope.Dispatch` surface.
- Engine drain explicitly owns per-type routing (was implicitly distributed).
- `actions ↔ triggers` cycle (FEATURE-MAP cycle observation) dissolves. Dependency graph becomes a DAG.

### Cascade docs

- `engine/01-game-engine.md` — orchestration section: per-type routing table, drain switch shape, holds `ITriggerPipeline` reference, no `FilterTriggers` call.
- `engine/03-game-rules-runtime.md` — retire `FilterTriggers` from the runtime surface.
- `actions/01-command-pipeline.md` — drain mechanic moves to engine. Feature becomes "validator chain + handlers + envelope shape."
- `actions/06-command-validator.md` — chain applies to `GameAction`, `Request`, `Response`; not `GameSignal` or `CompositeAction`.
- `triggers/triggers.md` — engine holds `ITriggerPipeline`; trigger eligibility via `Trigger<T>.Validate`; no `FilterTriggers`.
- `triggers/01-trigger.md` — `Validate` is THE eligibility surface.
- `triggers/02-trigger-pipeline.md` — in-memory registry; engine-driven invocation.
- `effects/01-effect.md` + `effects/02-effect-scope.md` — single `scope.Dispatch`.
- `engine.md` + `actions.md` feature roots — refreshed.
- FEATURE-MAP — dependency graph + cycle observation update.
- GLOSSARY — retire `FilterTriggers`, `scope.Emit`, `scope.Execute`.

## Status

Accepted. Cascade lands in the same PR.
