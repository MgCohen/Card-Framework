---
type: spike
status: proposed
created: 2026-10-31
references:
  - docs/architecture.md
  - docs/lookup-patterns.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/command-service.md
  - Product/spikes/request-service.md
  - Product/spikes/hook-config-design.md
sandbox: Assets/Framework/Commands/
tags: [#spike, #pipeline, #commands, #effects, #intents, #engine-loop]
---

# Layered command pipeline

> **Status: proposed.** Design only. No code changes. This spike covers the pipeline shape that intent, effect and command share, and the pump that schedules them. The type-keyed router, record ids and the block system are separate spikes. Section 9 states what this piece needs from them.

## 1. TL;DR

1. One `Pipeline<TBase, TScope>` runs **one** message: gates, validator, open block, Before hooks, handler, After hooks, close block. The scheduler and replication stay **outside** the pipeline. The handler count is always one, because events do not use the pipeline.
2. Logic lives in **DI-registered handlers** at every level (command-service L4). Messages are data and carry typed ids (`EntityId`, `RequestId`), not live `Entity` refs.
3. The core is **async on `Task`**. One single-threaded `Pump` runs one chain at a time with a null `SynchronizationContext`. Before hooks are awaited inline. After hooks push frames onto the effect stack.
4. `Pump.Enqueue` is the only entry. It owns a FIFO intent queue, the effect stack and the SBA fixpoint (CR 117.5). A client `Response` travels as an `Answer` intent. The pump admits it while a chain waits on a request.
5. Hooks exist only at the command level. Game-wide hooks are frozen DI registrations. Card hooks go in a server-only `HookTable`. Dispatch reads a snapshot.

### Decisions (2026-10-31)

The user made these decisions for all four infra spikes:

1. Ids are typed `readonly struct`s around `int`: `EntityId`, `RequestId`, `BlockId`, `MessageId` (`infra-record-ids.md`). Messages and scopes use typed ids, not `int`.
2. `TypeRouter<T>`, `TypeMultiRouter<T>`, `IdAllocator<TId>` and `EntityId` live in `Framework.Core`.
3. A new `Framework.Blocks` feature owns all `Block*` types. The pipeline opens and closes blocks only through `BlockService.Run` / `RunAsync` (`infra-block-wire.md`).
4. The async core uses `System.Threading.Tasks.Task`. Step 2b adds a test that proves continuations run inline on the pump in EditMode and in PlayMode.
5. The wire carries `CauseId` only. The nesting parent comes from bracket order. A stack frame captures `BlockService.Current` as its `CauseId` when it is pushed. There is no parent override.
6. An intent opens no block.
7. Hooks exist only at the command level.

## 2. Current shapes

### Framework (`Assets/Framework/`)

| Level | Input | Output | Lookup | Customization | Scheduling |
|---|---|---|---|---|---|
| Intent | none | none | none | none | none. `GameEngine` has no `Enqueue` (`Engine/Runtime/GameEngine.cs:10-42`). |
| Effect | none | none | none | none | none. `BlockKind.Effect` is unused (`Wire/Runtime/Events/BlockKind.cs`). |
| Command | `Command` (`Commands/Runtime/Command.cs:3`) | `CommandResult` (`CommandResult.cs:3`) | `CommandHandlerRegistry.HandlerFor` by `cmd.GetType()` (`CommandHandlerRegistry.cs:22`, `CommandService.cs:31`); opt-in `CommandValidatorRegistry.TryFor` (`CommandValidatorRegistry.cs:24`) | DI `CommandHandler<T>` (`CommandHandler.cs:5`), `CommandValidator<T>` (`CommandValidator.cs:5`), `RegisterCommandHandler<T>` (`Bootstrap/Runtime/HandlerRegistrationExtensions.cs:14`) | Inline, sync (`CommandService.cs:21-40`). Block `Open`/`Close` at `CommandService.cs:28,35,38`. `ParentId` is always 0 (`Wire/Runtime/Events/BlockScope.cs:15`). |
| Event | `WireEvent` | store write | `HandlerRegistry` fan-out list by type (`Wire/Runtime/Stream/HandlerRegistry.cs:22-26`) | DI `WireEventHandler<T>` (`WireEventHandler.cs:5`) | Inline inside `EventStream.Emit` (`EventStream.cs:15-19`). `WireForwarder` replicates. |
| Request (await point) | `Request<TResponse>` | `Task<TResponse>` (`Requests/Runtime/RequestService.cs:18`) | `_pending` by id (`RequestService.cs:10`) | game apply handlers | `Complete` (`RequestService.cs:28`) runs from a Response **apply handler** (`Requests/Tests/TestPickHandlers.cs:44`). |

### VerticalSlice (`Assets/Spike/ECS/VerticalSlice/Engine/`)

| Level | Input | Output | Lookup | Customization | Scheduling |
|---|---|---|---|---|---|
| Intent | `Command` (`Pipeline/GameEngine.cs:81`) | none | none | none | FIFO `Queue<Command>` (`GameEngine.cs:52`), `Kick` (`:87`), `RunPumpAsync` (`:96`) |
| Effect | `Effect` (`Effects/Effect.cs:18-19`) | none (`Validate` false = no block) | none: logic on the message (`Run`) | subclass `Effect`; virtual `Stack.ShouldResolve` (`Pipeline/Stack.cs:80`) and `ResolveAsync` (`:91`); settable `GameEngine.Stack` (`GameEngine.cs:50`); `Stack.OnCastResolved` delegate (`Stack.cs:70`) | LIFO `Stack.Push` (`Stack.cs:58`), `ResolveNextAsync` (`:121`); inline `EffectService.RunInline` (`Pipeline/EffectService.cs:84`) |
| Command | `Command` (`Commands/Command.cs:22`, `Apply` on message) | none | none: logic on the message | subclass `Command`; hooks via `EffectStore.Subscribe` (`Pipeline/EffectStore.cs:47`) + `HookConfig.IsActive` (`HookConfig.cs:53`) | Inline `CommandService.Execute` (`Pipeline/CommandService.cs:59`). Before hooks drain the stack sync with `GetAwaiter().GetResult()` (`:109`). |
| Event | `EventLog.Append` | log only | none | none | inline |
| SBA | `ISBA.Evaluate` (`Pipeline/ISBA.cs:34`) | `IEnumerable<Command>` | list | `RegisterSBA` | fixpoint in `EvaluateAsync` (`GameEngine.cs:121`), `RunSBAPass` (`:138`) |
| Request | `Request` (`Effects/Request.cs:33`, sync `Resolve` callback) | none | `_open` by id | subclass `Request` | `Respond` runs the resolution inline (`Pipeline/RequestsService.cs:69`) |

**Findings that shape the design.**

- VerticalSlice scopes leak engine objects. `ICommandScope` exposes `EffectStore` and `Stack` (`Commands/ICommandScope.cs:47,53`). A command can push anything and read the hook table.
- The sync drain at `CommandService.cs:109` blocks if a Before hook awaits. The new design awaits Before hooks inline and does not use the stack for them. The wire result is the same.
- The Framework `Complete` call inside an apply handler is a hazard. The awaiting code resumes **inside** `EventStream.Emit`, before the other handlers and `WireForwarder` see the Response. The new design calls `Complete` after `Emit` returns, from the pump. This also removes an ARCH-06 conflict (apply handlers do not call services).

## 3. Generalized contract

### What is generic, what is a level parameter

The proposal named four differences: scheduler, handler count, scope, replication. Only the scope belongs inside the pipeline.

| Difference | Where it goes | Reason |
|---|---|---|
| Scheduler | `Pump` (outside) | The pipeline runs one message. The pump decides **when**. |
| Handler count | fixed at 1 | Only events fan out. Events keep `HandlerRegistry` (section 6). |
| Scope | `LevelConfig` | Each level gets different downward verbs. |
| Replication | outside | Only events replicate (`WireForwarder`). Intents travel client to server in phase 2 (wire spike). |

`LevelConfig` also carries the block kind, the level gates and the hook dispatch.

### Pipeline core

Folder `Runtime/Pipeline/` in the command feature. One type for each file. Bridges copy the `WireEventHandler<T>` pattern.

```csharp
public interface IHandler<TBase, in TScope>
{
    Type MessageType { get; }
    Task Handle(TBase message, TScope scope);
}

public abstract class Handler<T, TBase, TScope> : IHandler<TBase, TScope> where T : TBase
{
    public Type MessageType => typeof(T);
    public abstract Task Handle(T message, TScope scope);
    Task IHandler<TBase, TScope>.Handle(TBase message, TScope scope) => Handle((T)message, scope);
}

public interface IValidator<TBase>
{
    Type MessageType { get; }
    bool Validate(TBase message, out string reason);
}

public interface IGate<TBase>
{
    bool Admit(TBase message, out string reason);
}

public interface IHookDispatch<TBase>
{
    Task Before(TBase message);
    void After(TBase message);            // pushed frames capture BlockService.Current as CauseId
}

public interface IScopeFactory<TScope>
{
    TScope Create();
}

public readonly struct Outcome { /* same shape as CommandResult: Accepted, RejectReason, Accept(), Reject(reason) */ }

public sealed class LevelConfig<TBase, TScope>
{
    public BlockKind                   Block  { get; }   // null = the level opens no block (intent)
    public IReadOnlyList<IGate<TBase>> Gates  { get; }
    public IHookDispatch<TBase>        Hooks  { get; }
    public IScopeFactory<TScope>       Scopes { get; }

    public LevelConfig(BlockKind block, IReadOnlyList<IGate<TBase>> gates,
                       IHookDispatch<TBase> hooks, IScopeFactory<TScope> scopes)
    { Block = block; Gates = gates; Hooks = hooks; Scopes = scopes; }
}

public sealed class Pipeline<TBase, TScope> where TBase : class
{
    readonly LevelConfig<TBase, TScope> _level;
    readonly TypeRouter<IHandler<TBase, TScope>> _handlers;     // Framework.Core
    readonly TypeRouter<IValidator<TBase>> _validators;
    readonly BlockService _blocks;                              // Framework.Blocks

    public Pipeline(LevelConfig<TBase, TScope> level, TypeRouter<IHandler<TBase, TScope>> handlers,
                    TypeRouter<IValidator<TBase>> validators, BlockService blocks)
    { _level = level; _handlers = handlers; _validators = validators; _blocks = blocks; }

    public Task<Outcome> Run(TBase message) => Run(message, BlockId.None);

    // causeId: set only when a stack frame resolves; BlockId.None = the lexical parent.
    public async Task<Outcome> Run(TBase message, BlockId causeId)
    {
        var type = message.GetType();
        // Gates and validator run before the block: a reject emits nothing.
        for (var i = 0; i < _level.Gates.Count; i++)
            if (!_level.Gates[i].Admit(message, out var gateReason)) return Outcome.Reject(gateReason);
        if (_validators.TryFor(type, out var v) && !v.Validate(message, out var reason))
            return Outcome.Reject(reason);

        var handler = _handlers.For(type);
        if (_level.Block == null)
        {
            await handler.Handle(message, _level.Scopes.Create());
            return Outcome.Accept();
        }

        // RunAsync owns open, close, Errored and Cancelled.
        return await _blocks.RunAsync(BlockHeader.Of(_level.Block, message, causeId), async () =>
        {
            await _level.Hooks.Before(message);
            await handler.Handle(message, _level.Scopes.Create());
            _level.Hooks.After(message);
            return Outcome.Accept();
        });
    }
}
```

`BlockHeader` carries no message-type slot: the block spike dropped the `Message` field. `BlockHeader.CauseId` is typed `BlockId` under decision 1.

### Levels

| Level | `TBase` | `TScope` | Block | Gates | Hooks | Scheduler |
|---|---|---|---|---|---|---|
| Intent | `Intent` | `IIntentScope` | none (`null`) | system gate + game gates | none | FIFO `IntentQueue` |
| Effect | `Effect` | `IEffectScope` | `EffectBlock` | none | none | LIFO `EffectStack` + `IStackPolicy`; inline through `Run` |
| Command | `Command` | `ICommandScope` | `CommandBlock` | none | `CommandHookDispatch` | inline |

The intent level opens **no** block (decision 6). Reason: an `Answer` resumes the waiting chain inline. If the `Answer` had its own block, the resumed command would close its block while the `Answer` block is still open, and the block stack would break LIFO. `IntentBlock` stays registered in `BlockKindRegistry` but the pipeline does not use it.

The base classes are data only. `Effect` implements `ISourced` from `Framework.Blocks`, so `BlockHeader.Of` reads its source. Each level keeps a named handler base, so authors keep the current names:

```csharp
public abstract class Intent  { public EntityId SenderId; }
public abstract class Effect : ISourced { public EntityId Source; EntityId ISourced.SourceId => Source; }
public abstract class Command { }

public readonly struct StackFrame
{
    public readonly MessageId Id;        // from IdAllocator<MessageId> at push
    public readonly Effect    Effect;
    public readonly BlockId   CauseId;   // BlockService.Current at push
}

public abstract class IntentHandler<T>  : Handler<T, Intent,  IIntentScope>  where T : Intent  { }
public abstract class EffectHandler<T>  : Handler<T, Effect,  IEffectScope>  where T : Effect  { }
public abstract class CommandHandler<T> : Handler<T, Command, ICommandScope> where T : Command { }
```

`CommandValidator<T>` keeps its current shape and implements `IValidator<Command>`. `EffectValidator<T>` and `IntentValidator<T>` copy it.

### Scopes

A scope holds **only the downward verbs** of its level. This refines the proposal: reads do not go through the scope. Handlers ctor-inject `IWorldView` or a single read-only store. `IWorldView` lives in `Framework.Engine` and holds the four read-only views that `GameEngine` exposes today (`IReadOnlyEntityStore`, `IReadOnlyTagStore`, `IReadOnlyStateStore`, `IReadOnlyRequestStore`). This keeps one way to read (DI, as `DealDamageValidator` does now). It also keeps `Framework.Commands` free of references to Tags, Entities and States.

```csharp
public interface IIntentScope
{
    Task<Outcome> Execute(Command command);
}

public interface IEffectScope
{
    Task<Outcome> Execute(Command command);                                 // level below
    Task<Outcome> Run(Effect effect);                                       // same level, inline, nested block
    Task<TResponse> Ask<TResponse>(Request<TResponse> request) where TResponse : Response;
}

public interface ICommandScope
{
    void Push(Effect effect);                                               // schedule; CauseId = BlockService.Current
    Task<TResponse> Ask<TResponse>(Request<TResponse> request) where TResponse : Response;
}
```

| Scope | Verbs | Not allowed |
|---|---|---|
| `IIntentScope` | `Execute(Command)` | services, `Push`, `Ask` |
| `IEffectScope` | `Execute(Command)`, `Run(Effect)`, `Ask` | services (a direct `TagService` call skips the command hooks) |
| `ICommandScope` | `Push(Effect)`, `Ask`; services by ctor injection | `Execute(Command)`, `Run(Effect)` |
| apply handler | none; writes its store | services (ARCH-06) |

`Push` is not a call into the level above. It schedules a frame. The frame runs only when the pump settles.

### Schedulers and the pump

```csharp
public interface IStackPolicy
{
    bool ShouldResolve(IReadOnlyList<StackFrame> frames);
    Task OnResolved(StackFrame frame, IEffectScope scope);
}

public interface IPumpPolicy
{
    bool Admits(Intent intent, PumpStatus status);
}

public interface IStateCheck
{
    IEnumerable<Command> Evaluate();
}

public sealed class Pump
{
    public Task<Outcome> Enqueue(Intent intent);
    public Task<Outcome> Enqueue(Command command);   // server origin; wraps in a framework RunCommand intent
}
```

Defaults: `AutoResolveStackPolicy` (any frame resolves, as in the slice), `AnswersOnlyWhileSuspended` pump policy, and no state checks. Games replace them in DI. No engine-owned type has a virtual method.

The pump drives one chain at a time:

```csharp
void Kick()
{
    if (_inKick) return;
    _inKick = true;
    var saved = SynchronizationContext.Current;
    // Null context: an awaited Ask resumes inline on this call stack, in a fixed order.
    SynchronizationContext.SetSynchronizationContext(null);
    try
    {
        while (true)
        {
            if (_step != null && !_step.IsCompleted)
            {
                if (!_intents.TryTake(_policy, PumpStatus.Suspended, out var answer)) return;
                RunAnswer(answer);            // Emit Response, then Complete: _step resumes here
                continue;
            }
            Observe(_step);                   // rethrows a handler fault to the Enqueue task
            _step = null;
            if (_intents.TryTake(_policy, PumpStatus.Idle, out var next)) { _step = Cycle(next); continue; }
            _step = Settle();                 // frames pushed from outside a cycle
            if (_step.IsCompleted) { Observe(_step); _step = null; return; }   // at rest
        }
    }
    finally { SynchronizationContext.SetSynchronizationContext(saved); _inKick = false; }
}

async Task Cycle(QueuedIntent item) { item.Complete(await _intentPipeline.Run(item.Intent)); await Settle(); }

async Task Settle()                            // MTG CR 117.5, same loop as the slice EvaluateAsync
{
    while (true)
    {
        while (await RunStateCheckPass()) { }   // SBA fixpoint; emitted commands run inline
        if (!await _stack.ResolveNext()) return;  // one frame, then SBA again
    }
}
```

Composition rule: the command scheduler is inline. The stack fills only from `ICommandScope.Push` and from After hooks. The intent queue fills only from `Enqueue`. `Settle` always runs to rest before the next intent leaves the queue.

## 4. Example end to end

Divine Shield target, client plays Fireball. Ids are `int`.

Every `*Id` field below is an `EntityId`.

```csharp
sealed class PlayCardIntentHandler : IntentHandler<PlayCardIntent>
{
    public override Task Handle(PlayCardIntent i, IIntentScope s)
        => s.Execute(new PlayCard { CardId = i.CardId, CasterId = i.SenderId, TargetId = i.TargetId });
}

sealed class PlayCardHandler : CommandHandler<PlayCard>       // ctor: ZoneService, CastFactory
{
    public override Task Handle(PlayCard c, ICommandScope s)
    {
        _zones.Move(c.CardId, Zones.Limbo);
        s.Push(_cast.Build(c.CardId, c.CasterId, c.TargetId)); // new Fireball { Source, TargetId }
        return Task.CompletedTask;
    }
}

sealed class FireballHandler : EffectHandler<Fireball>
{
    public override Task Handle(Fireball e, IEffectScope s)
        => s.Execute(new DealDamage { SourceId = e.Source, TargetId = e.TargetId, Amount = 6 });
}

sealed class DivineShieldHook : CommandHook<DealDamage>       // ctor: IReadOnlyTagStore
{
    public DivineShieldHook() : base(new HookConfig(HookPhase.Before, HookPriority.Replacement)) { }
    public override Effect Build(DealDamage cmd)
        => cmd.Amount > 0 && HasShield(cmd.TargetId) ? new BreakShield { Trigger = cmd } : null;
}

sealed class BreakShieldHandler : EffectHandler<BreakShield>
{
    public override Task Handle(BreakShield e, IEffectScope s)
    {
        e.Trigger.Amount = 0;                                    // replacement: edit the in-flight command
        return s.Execute(new RemoveDivineShield { EntityId = e.Trigger.TargetId });
    }
}
```

| # | What runs | Level | Wire |
|---|---|---|---|
| 1 | `Enqueue(PlayCardIntent)`: system gate, `PlayCardIntentHandler` | intent (FIFO) | none |
| 2 | `PlayCard` validator, block, `PlayCardHandler`: move to limbo, `Push(Fireball)` | command (inline) | `BlockStart#1 Command` · `ZoneMove` · `BlockEnd#1` |
| 3 | `Settle`: no SBA; stack pops `Fireball`. Its frame captured `CauseId` #1 at push. | effect (LIFO) | `BlockStart#2 Effect cause 1` (depth 0) |
| 4 | `FireballHandler` executes `DealDamage` | command | `BlockStart#3 Command` (nested in #2, cause = lexical parent) |
| 5 | Before hook: `DivineShieldHook.Build` returns `BreakShield`. The dispatcher awaits it inline. | effect (inline) | `BlockStart#4 Effect` (nested in #3) |
| 6 | `BreakShieldHandler`: `Amount = 0`, executes `RemoveDivineShield` | command | `BlockStart#5` · `TagSet DivineShield=false` · `BlockEnd#5` · `BlockEnd#4` |
| 7 | `DealDamageHandler`: `TagService.Set(health - 0)`. Same value, no event. | command | none |
| 8 | After hooks for `DealDamage`: none active | command | `BlockEnd#3` · `BlockEnd#2` |
| 9 | `IStackPolicy.OnResolved(frame)`: game executes `MoveCard` to graveyard | effect scope | `BlockStart#6 Command` · `ZoneMove` · `BlockEnd#6` |
| 10 | `Settle`: SBA pass empty, stack empty. Pump at rest. `Enqueue` task completes with `Accepted`. | pump | none |

Events (`TagSet`, `ZoneMove`) are the event level. `HandlerRegistry` applies them on the server, and `WireForwarder` sends them to the client. The same apply handlers write the client stores.

If the effect awaits a choice (`await s.Ask(new PickTarget{...})`), the step stays incomplete. The pump returns from `Kick`. The client answer arrives as `Enqueue(Answer)`. The pump policy admits it, `RunAnswer` emits the Response event and then calls `Complete`. The effect resumes inside that `Kick`.

## 5. Call-site migration

| Current type | New type | Change needed |
|---|---|---|
| `Framework.Commands.Command` | same | none |
| `CommandHandler<T>.Handle(T)` (sync) | `CommandHandler<T> : Handler<T, Command, ICommandScope>`, `Task Handle(T, ICommandScope)` | Step 1: none (base bridges). Step 2: signature change, return `Task.CompletedTask`. |
| `ICommandHandler` | `IHandler<Command, ICommandScope>` | registration helper keeps its name |
| `CommandHandlerRegistry`, `CommandValidatorRegistry` | `TypeRouter<IHandler<Command, ICommandScope>>`, `TypeRouter<IValidator<Command>>` (`Framework.Core`) | Step 1: replaced; handlers and validators implement `IRouted` (router spike). |
| `CommandValidator<T>` | same, implements `IValidator<Command>` | none |
| `CommandResult` | `Outcome` | Step 1: `Execute` maps `Outcome` to `CommandResult`. Step 2: rename at call sites. |
| `CommandService.Execute` | `Pipeline<Command, ICommandScope>.Run` (internal), `Pump.Enqueue(Command)` (external) | Step 2: `CommandService` deleted. |
| `GameEngine.Commands` | `GameEngine.Enqueue(...)` | Step 2. ARCH-07 allowed list gets `Task`. |
| `MatchHarness.Execute`, `MatchPair.Execute` | same name; body calls `Enqueue` and asserts the task completed | Step 2. Bindings do not change. |
| `CommandInstaller` | `PipelineInstaller` (pump, levels, policies) | Step 2 |
| Response apply handler calls `RequestService.Complete` | built-in `Answer` intent; pump emits, then completes | Step 2. The apply handler only removes from the store. |
| `RequestService.Open` from game code | `ICommandScope.Ask` / `IEffectScope.Ask` | Step 2 |
| VS `Command.Apply`, `Effect.Validate` / `Run`, `Effect<TPayload>.Trigger` | `CommandHandler<T>`, `EffectValidator<T>` / `EffectHandler<T>`, a field on the effect | phase 4 port |
| VS `Stack` virtuals, `GameEngine.Stack` setter, `OnCastResolved` | `IStackPolicy` in DI | phase 4 |
| VS `EffectStore.Subscribe`, `HookConfig`, `HookContext` | DI `CommandHook<T>` or `HookTable.Subscribe`; same config shape with a constructor (no `init`); `HookContext` holds `Command` and `IWorldView` | phase 4 |
| VS `EffectStore.SetDirect` / `FireDirect` | stays specialized (section 6) | phase 4 |
| VS `ISBA` | `IStateCheck` (reads by ctor injection) | phase 4 |
| VS `RequestsService.Respond` + `Request.Resolve` | `Enqueue(Answer)` + `await scope.Ask(...)` | phase 2 / 4 |

## 6. What stays specialized

- **Event level.** It reuses only the router idea, as `HandlerRegistry`. It fans out to N handlers, it has no validator, no block and no scope, and it runs on both sides. Forcing it into `Pipeline<,>` would add a scope and a block that it never uses. It stays in Wire.
- **Requests.** A request is an await point, not a level. `RequestService` keeps id allocation and the TCS table. The scopes only forward to it, so the pump can see the suspension.
- **Direct triggers.** `SetDirect` / `FireDirect` (Deathrattle) is an entity-keyed table, not a type route. It is game content behind an After hook (`DeathrattleBridge`). It stays a game-side helper over `HookTable` until `oq-trigger-subscription-entry` is answered.
- **State checks (SBA).** They are not a level. They are a pump step that emits commands. They have no message type to route.
- **The `Answer` path.** It is pump code, not an `IntentHandler`. It must run while a chain is suspended and must call `Complete` only after `Emit` returns.
- **Hook storage.** Routers are frozen. Game-wide hooks are frozen too (`CommandHook<T>` in DI, sorted by priority at composition). Card hooks need live add and remove, so they go in a server-only `HookTable`. It is not a Store in the lookup-patterns sense: it is not on the wire, like `RequestService._pending`. Dispatch copies the matching list at the start of each phase, so a change during a block applies from the next dispatch. `HookConfig.IsActive` stays the main gate, as `hook-config-design.md` decided.

## 7. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Check kind | Params (sketch) |
|---|---|---|---|
| PIPE-01 | A scope interface exposes only the verbs of its level. | `type.memberTypes` **extended** with `includeParameters: true` (today it checks return, field and property types only) | one rule entry for each scope. `ICommandScope`: allowed `Effect`, `Request*`, `Response`, `Task*`, `Void`. `IEffectScope`: `Command`, `Effect`, `Request*`, `Response`, `Task*`, `Outcome`. `IIntentScope`: `Command`, `Task*`, `Outcome`. |
| PIPE-02 | An effect handler does not call a Framework service. | `il.forbiddenCalls` (exists) | callers `subclassOf: Framework.Commands.EffectHandler\`1`; callees `type: *Service` |
| PIPE-03 | An intent handler does not call a Framework service. | `il.forbiddenCalls` | callers `IntentHandler\`1`; callees `*Service` |
| PIPE-04 | A handler does not call the pipeline or the pump. It uses its scope. | `il.forbiddenCalls` | callers `subclassOf: Handler\`3`; callees `type: Pipeline\`2`, `Pump` (needs a `types[]` param, or two entries) |
| PIPE-05 | A handler does not leave the pump thread. | `il.forbiddenCalls` **extended** with a `methods[]` filter | callers `Handler\`3`; callees `System.Threading.Tasks.Task::Run`, `Task::Delay`, `Task::ConfigureAwait` |
| ARCH-02 / ARCH-07 | table updates, not new rules | existing | ARCH-02: Commands gets `Framework.Requests` (for `Ask`). ARCH-07: `GameEngine` allowed list gets `Task`. |

PIPE-01 is the rule the brief asks for. It catches a scope that grows an `EffectStore` or `Stack` property, as in the slice. PIPE-02 to PIPE-04 close the bypass: a handler that cannot reach a lower level through its scope must not reach it through DI.

## 8. Migration steps

Each step lands as one PR and keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green.

**Phase 1 — engine loop**

Steps 1 and 2 come after the router spike step that adds `TypeRouter<T>` to `Framework.Core`, the record-ids steps that add `EntityId`, `RequestId` and `BlockId`, and the block spike step that adds `Framework.Blocks` with `BlockService`.

1. **Command level onto the pipeline. No behavior change.** Add `Runtime/Pipeline/` to `Framework.Commands`: `Pipeline<,>`, `LevelConfig<,>`, `IHandler<,>`, `Handler<,,>`, `IValidator<>`, `IGate<>`, `IHookDispatch<>` (with a no-op), `IScopeFactory<>`, `Outcome`. `CommandHandler<T>` keeps its abstract `void Handle(T)`. Its explicit `IHandler` member calls it and returns `Task.CompletedTask`. `CommandService.Execute` runs the pipeline. It throws if the task is not complete, and maps `Outcome` to `CommandResult`. The pipeline uses `TypeRouter<T>` and `BlockService.RunAsync`. Proof: `CommandServiceTests` and all acceptance bindings pass unchanged. Add `PipelineTests` (reject emits nothing, errored close, order of phases).
2. **Async commands, `Ask`, pump.** Switch `CommandHandler<T>` to `Task Handle(T, ICommandScope)`. Add `ICommandScope.Ask`, `Pump`, `Enqueue(Command)`, the FIFO queue, `IdAllocator<MessageId>`, the `Answer` path and `GameEngine.Enqueue`. Delete `CommandService`. `MatchHarness.Execute` calls `Enqueue`. Add PIPE-01, PIPE-04 and PIPE-05, and the ARCH-02 and ARCH-07 table updates. Proof: a new acceptance picture where a command awaits a request; the blocks nest by bracket order.
2b. **Inline continuation test (decision 4).** Add an EditMode test in `Framework.Commands.Tests` and a PlayMode test in a new `Framework.Commands.PlayTests` assembly. Each test enqueues a command that awaits `Ask`, enqueues the `Answer`, and asserts three facts: the command finished before `Enqueue(Answer)` returned, it ran on the main thread, and the stream order is `Response` before the command's later events. The PlayMode test runs with `UnitySynchronizationContext` installed. Run the gate with `-With playmode`. If PlayMode fails, the pump keeps the null-context switch from section 3 and the test proves it.
3. **Intent level and the system gate.** Add `Intent`, `IntentHandler<T>`, `IIntentScope`, `IGate<Intent>` with a default `StateGate` (`oq-system-gate-validation`), and `IPumpPolicy`. `Enqueue(Command)` wraps into the built-in `RunCommand` intent, which skips game gates. Add PIPE-03. Proof: an intent in a state that does not allow it emits nothing.

**Phase 2 — client submit**

4. **Intents from the client.** The wire spike serializes intents and stamps `SenderId` on the server. `Answer` arrives from the client the same way. Proof: a `MatchPair` test plays an intent and answers a request from the client side, and `AssertStreamsMatch` passes.

**Phase 4 — reactions**

5. **Rename `Framework.Commands` to `Framework.Pipeline`.** Pure move, before effects land. Update ARCH-02, ARCH-04 and the namespaces.
6. **Effect level.** Add `Effect`, `EffectHandler<T>`, `EffectValidator<T>`, `IEffectScope`, `EffectStack`, `IStackPolicy` and the stack part of `Settle`. Add `ICommandScope.Push`. Add PIPE-02. Proof: the Fireball picture (steps 2–4 of section 4).
7. **Command hooks.** Add `CommandHook<T>`, `HookConfig`, `HookContext`, `HookTable` and `CommandHookDispatch`. Before hooks are awaited inline. After hooks push frames whose `CauseId` is the command block (`BlockService.Current` at push). Proof: the Divine Shield picture; a Backlash After hook resolves once (L+3).
8. **State checks.** Add `IStateCheck` and the SBA fixpoint in `Settle`. Proof: the VerticalSlice Deathrattle scenario as an acceptance test (phase 4 "done when").

## 9. Interfaces needed from the other pieces

**Router** (`infra-type-router.md`, `Framework.Core`). The pipeline uses `TypeRouter<T>` for handlers and validators and `TypeMultiRouter<T>` for frozen hooks and gates. Handler, validator and hook bases implement `IRouted`.

| Use | Needs |
|---|---|
| handlers | `For(Type)`, throws on a miss; a duplicate throws at composition |
| validators | `TryFor(Type, out T)`, false on a miss (opt-in) |
| frozen hooks, gates | `All(Type)`, stable order by priority, then registration order; no allocation |
| `HookTable` | the same read shape (`IRouteSource<T>`), so dispatch merges frozen and live hooks with one loop |

Exact-type match is enough for steps 1–8. Base-type match is an open question.

**Ids** (`infra-record-ids.md`):

- Messages carry `EntityId` (`Framework.Core`). The pipeline needs a cheap "id is alive" check for validators, once destruction exists (`oq-entity-destruction`).
- `RequestId` stays with `RequestService`. `Ask` returns the typed response; the pump matches `Answer` by `RequestId`.
- `MessageId` and `IdAllocator<MessageId>` live in the pipeline assembly. `Enqueue` allocates one for each queued intent and each pushed `StackFrame`. It is local and not on the wire.
- Phase 2 needs a per-sender sequence number on `Intent`, so the server can reply to a submit with its `Outcome`.

**Blocks** (`infra-block-wire.md`, feature `Framework.Blocks`):

- `BlockService.RunAsync(BlockHeader, Func<Task<T>>)` opens, runs the body and closes with `Completed`, `Errored` or `Cancelled`. `Pipeline.Run` has no open or close code of its own.
- `BlockService.Current`: the innermost open block, `BlockId.None` when none.
- `BlockHeader.Of(kind, message, messageSlot, causeId)`. `CauseId` is `BlockId` under decision 1 (the block draft still shows `int`). `BlockId.None` means "cause = lexical parent".
- There is no parent override. The nesting parent comes from bracket order. A stack frame captures `Current` as `CauseId` at push, and passes it to `Run(message, causeId)` when it resolves.
- `CommandBlock` and `EffectBlock` from `BlockKindRegistry`. `IntentBlock` is not used (decision 6).
- The open-block stack stays valid across an `await`. This holds because the pump runs one chain at a time and the intent level opens no block.

## 10. Open questions

1. **Replacement by edit.** Divine Shield edits the in-flight command (`Amount = 0`), as the slice does. Alternative: a Before hook returns a replacement command. Edit is simpler, but the hook order then decides the result.
2. **Card hooks: live table or derived from state?** A server-only `HookTable` is not replayable from the wire. Deriving active hooks from definitions plus zone at dispatch is replayable, but needs the zone store (phase 3).
3. **Same-level calls.** `IEffectScope.Run(Effect)` is allowed and a command cannot execute a command. Confirm both.
4. **Concurrent decisions.** The default pump admits only answers while suspended. Simultaneous mulligan needs two open requests and one chain. Is that a policy, or a different scheduler?
5. **Base-type routing** for gates and validators (command-service deferred item "validator fan-out").
6. **Handler per effect type.** Many cards each need one effect type and one handler. Is this acceptable before `oq-effect-authoring` (phase 6) gives a data form?
7. **Trigger order** (`oq-trigger-ordering`). After hooks push in priority order, so the last pushed resolves first. Is LIFO within one priority band correct for the target games?
8. **Message slot (deferred).** The block spike dropped `BlockHeader.Message`. If presentation later needs the message type on the wire, decide which registry gives the slot (block spike, open question 8).

Closed on 2026-10-31: `Task` or UniTask (decision 4: `Task`, proved by step 2b); intent blocks (decision 6: no block); block parent override (decision 5: `CauseId` only); hooks on other levels (decision 7: command only).
