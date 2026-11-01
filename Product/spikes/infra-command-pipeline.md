---
type: spike
status: proposed
created: 2026-10-31
updated: 2026-11-01
references:
  - docs/architecture.md
  - docs/lookup-patterns.md
  - Product/spikes/framework-roadmap.md
  - Product/spikes/infra-type-router.md
  - Product/spikes/infra-record-ids.md
  - Product/spikes/infra-block-wire.md
  - Product/spikes/command-service.md
  - Product/spikes/request-service.md
  - Product/spikes/hook-config-design.md
sandbox: Assets/Framework/Commands/
tags: [#spike, #pipeline, #commands, #effects]
---

# Pipeline

> **Status: proposed.** Design only. No code changes. Page: https://claude.ai/artifact/74vPp64abx63ubpagvdpAg (catalog: https://claude.ai/artifact/Y1iBedUs8htkxDpkg6QYGU).

## 1. Summary

1. `Pipeline<TMessage, TScope>` runs one message through a fixed order: `Validate`, `CreateScope`, `Before`, `Handle`, `After`. It returns a `RunRecord`.
2. A pipeline holds a list of items. The owner system calls `RunNext()`. The pipeline selects the next item, removes it from the list and runs it. A scope can run a message inline through the `IInlineRunner<TMessage>` of its own pipeline.
3. v0 is **one pipeline** that runs on its own. Two or more pipelines are v2. The loop, blocks, ids, requests and the concrete pipelines are **out**. Section 2 names their owners.

### Decisions

From 2026-10-31 (all four infra spikes):

1. Ids are typed `readonly struct`s: `EntityId`, `RequestId`, `BlockId`, `MessageId` (`infra-record-ids.md`).
2. `TypeRouter<T>`, `TypeMultiRouter<T>`, `IdAllocator<TId>` and `EntityId` live in `Framework.Core`.
3. A new `Framework.Blocks` feature owns all `Block*` types (`infra-block-wire.md`).
4. The async core uses `System.Threading.Tasks.Task`.
5. The wire carries `CauseId` only. The nesting parent comes from bracket order.
6. An intent opens no block.
7. Hooks exist only at the command level. Only the command pipeline fills its `Before` and `After` stores.

From 2026-11-01 (this spike):

8. **Scope first.** The pipeline owns only the concerns marked "in" in section 2.
9. **API first.** Each part serves an entry point in section 4.
10. The loop is out. The owner system decides when to call `RunNext`.
11. `Execute` is not virtual. The steps in it are virtual: `Validate`, `Before`, `Handle`, `After`. `CreateScope` is abstract. `SelectNext` is virtual.
12. One public run entry: `RunNext()`. Inline runs skip `SelectNext`. Only scopes get the inline runner. The concrete pipeline decides if its scope exposes it.
13. Validators are a separate first slot. A reject runs no other step.
14. Every message is wrapped in a `PipelineItem<TMessage>`. It has little behavior now. Replace and cancel come later.
15. After hooks cannot cancel.
16. Handlers get services from DI. Scope verbs are not in DI.
17. The pipeline defines the read interfaces of its stores and fills them at composition. Live add and remove is out.
18. No tuple returns in the API. `out` parameters are acceptable.
19. **v0 is one pipeline.** No `PipelineSet`. A pipeline gives its own inline runner to its scope. Two or more pipelines are v2.
20. The result is a small `RunRecord`: `Status`, `Message`, `Reason`, `Steps`. Add more fields only when a use needs them.
21. Hook subscription is not a pipeline concern. The pipeline reads `IRouteSource<T>.All(type)`. The router decides how types match.

## 2. Scope

| Concern | In / Out | Owner if out |
|---|---|---|
| Fixed run order: `Validate` → `CreateScope` → `Before` → `Handle` → `After` → `RunRecord` | in | — |
| Validator slot, first and separate | in | — |
| `Before`, `Handle`, `After` as virtual methods; the defaults read the stores | in | — |
| Store read interfaces and their injection | in | — |
| Fill the stores at composition | in | — |
| Item wrapper `PipelineItem<TMessage>` | in | — |
| Result `RunRecord`: status, message, reason, steps | in | — |
| Item list: `Add`, `Remove`, `TryPeek`, `SelectNext` | in | — |
| Run entries: `RunNext`, and the own `IInlineRunner<TMessage>` for scopes | in | — |
| Scope: one for each item, not in DI, closed after the run | in | — |
| Two or more pipelines: wiring cycle, inline runs across pipelines | out (v2) | This spike, after v0 runs |
| Replace a message, cancel an item | out (later) | This spike; `PipelineItem` is the place |
| Hook subscription, base-type match | out | `infra-type-router.md` |
| Fill the stores at runtime (live add and remove) | out | Concrete pipeline or game; `HookTable` in `infra-type-router.md` |
| How a store works inside | out | `infra-type-router.md` |
| List changes on the wire (stack UI) | out | Blocks / wire |
| Block bracket, `CauseId`, wire form | out | `infra-block-wire.md` (later) |
| `MessageId`, `EntityId` | out | `infra-record-ids.md` |
| Loop and order across pipelines (SBA, stack, next intent) | out | Engine loop (`GameEngine`; no spike yet) |
| Intents, client input, the `Answer` path | out | Engine loop / Requests |
| `Ask`: request id, await, completion | out | `RequestService`; the scope only forwards |
| Concrete pipelines, their scope verbs and their hooks | out | Commands and Effects features |
| Event fan-out and replication | out | Wire (`EventStream`, `HandlerRegistry`) |

**Boundary.** The pipeline receives a message from `Add` or from an inline runner. It wraps the message in a `PipelineItem`, runs the item, and gives a `RunRecord` back to the caller. Handlers call services, and services emit events to `EventStream`.

## 3. Today

### Uses

| Use | Where | Does |
|---|---|---|
| Command execute | `Framework/Commands/Runtime/CommandService.cs:21` | validate, block, one handler; sync |
| Command + hooks | VS `Engine/Pipeline/CommandService.cs:59` | block, Before hooks, `Apply`, After hooks |
| Effect resolve | VS `Engine/Pipeline/EffectService.cs:84` | `Validate`, block, `Run` |
| Effect stack | VS `Engine/Pipeline/Stack.cs:121` | LIFO list, virtual `ShouldResolve` (`:80`) |
| Command queue | VS `Engine/Pipeline/GameEngine.cs:52,96` | FIFO list, drained by the pump |

### Current shapes

| | Framework command | VS command | VS effect |
|---|---|---|---|
| Message | `Command` (`Commands/Runtime/Command.cs:3`) | `Command` with `Apply` (`Commands/Command.cs:22`) | `Effect` with `Validate`, `Run` (`Effects/Effect.cs:18-19`) |
| Validate | `CommandValidatorRegistry.TryFor` (`:24`) | none | `effect.Validate` |
| Handler lookup | `CommandHandlerRegistry.HandlerFor` (`:22`) | none: logic on the message | none: logic on the message |
| Hooks | none | `EffectStore.Subscribe` (`EffectStore.cs:47`); Before drains with `GetResult()` (`CommandService.cs:109`) | none |
| Scope | none | `ICommandScope` leaks `EffectStore`, `Stack` (`ICommandScope.cs:47,53`) | `IEffectScope` |
| Result | `CommandResult` (`CommandResult.cs:3`) | none | none (`Validate` false = no block) |
| List | none | `Queue<Command>` (`GameEngine.cs:52`) | `Stack` (`Stack.cs:58,121`) |

### What repeats

| Common part | Seen in | Varies between uses |
|---|---|---|
| Validate first; a reject does nothing | FW command, VS effect | validator object or method on the message |
| Find the logic by type | FW command | DI handler or method on the message |
| Before, handler, After | VS command | only commands have hooks |
| A scope for the handler | VS command, VS effect | verbs; the VS scopes leak engine objects |
| A list and a "next" rule | VS stack, VS command queue | LIFO with a policy, or FIFO |
| A result to the caller | FW command | VS returns nothing |
| Block bracket | all three | out of scope (Blocks) |

## 4. API

### Entry points

| Audience | Entry point | Does |
|---|---|---|
| author | `Handler<T, TMessage, TScope>.Handle(T, TScope)` | the logic for one message type |
| author | `Validator<T, TMessage>.Validate(T, out string)` | the check before the run |
| author | `Hook<T, TMessage, TScope>.Run(PipelineItem<TMessage>, TScope)` | a Before or After step |
| composition | `builder.RegisterHandler<TMessage, TScope, THandler>()` and the same for validators and hooks | fills the stores |
| composition | `new CommandPipeline(stores)` | makes the one pipeline; not in DI |
| composition | subclass `Pipeline<TMessage, TScope>`: `CreateScope`, `SelectNext`, `Before`, `After` | the custom part of one pipeline |
| runtime | `Add`, `Remove`, `TryPeek`, `Items`, `RunNext` | the owner system uses the list |
| runtime | `IInlineRunner<TMessage>.Run(TMessage)` | a scope runs a message now, nested, in the same pipeline |

### Shape

```csharp
public abstract class Pipeline<TMessage, TScope> where TMessage : class
{
    public MessageId Add(TMessage message);                         // into the list
    public bool Remove(MessageId id);                               // false: absent, or already taken
    public bool TryPeek(out PipelineItem<TMessage> item);           // the item that RunNext takes
    public IReadOnlyList<PipelineItem<TMessage>> Items { get; }     // read-only view
    public Task<RunRecord> RunNext();                               // RunRecord.None if no item is selected
    protected IInlineRunner<TMessage> Inline { get; }               // this pipeline; a subclass gives it to its scope
}

public interface IInlineRunner<TMessage> { Task<RunRecord> Run(TMessage message); }   // scopes only

public sealed class PipelineItem<TMessage> { public MessageId Id { get; } public TMessage Message { get; } public ItemState State { get; } }

public sealed class RunRecord
{
    public static readonly RunRecord None;                          // SelectNext gave no item
    public RunStatus Status { get; }                                // None, Accepted, Rejected
    public object Message { get; }
    public string Reason { get; }                                   // set only if Rejected
    public IReadOnlyList<IPipelineStep> Steps { get; }              // validator, hooks, handler, in run order
}
```

### Call site

```csharp
sealed class PlayCardHandler : CommandHandler<PlayCard>       // ctor: ZoneService (DI)
{
    public override async Task Handle(PlayCard c, ICommandScope s)
    {
        _zones.Move(c.CardId, Zones.Limbo);                     // service: from DI
        await s.Run(new DrawCard { Player = c.CasterId });      // verb: only from the scope
    }
}
```

The owner system runs the list:

```csharp
while ((await commands.RunNext()).Status != RunStatus.None) { }
```

### Access

| Bypass route | What stops it |
|---|---|
| A handler injects a pipeline | Pipelines are not in DI. |
| A handler injects the owner system | Rulebook check PIPE-04. |
| An effect handler injects a service and skips command hooks (v2) | Rulebook check PIPE-02. The scope cannot stop this. |
| A handler keeps the scope and uses it later | The scope closes after the run. Its verbs then throw. |
| A subclass changes the run order | `Execute` is not virtual. |
| A caller runs a message inline without a scope | `Inline` is protected. Only `CreateScope` gives it out. |
| A game removes the running item | `Remove` returns false. The item left the list before the run. |

## 5. Parts

| Part | Replaces |
|---|---|
| `Pipeline<TMessage, TScope>` | FW `CommandService`, VS `CommandService.Execute`, VS `EffectService.RunInline` |
| Item list and `SelectNext` | VS `Stack`, VS `GameEngine` queue |
| `PipelineItem<TMessage>` and `RunRecord` | `CommandResult`; nothing in VS |
| Stores and author bases | `CommandHandlerRegistry`, `CommandValidatorRegistry`, VS `EffectStore` subscriptions, logic on messages |
| Scope | VS `ICommandScope`, `IEffectScope` |
| `IInlineRunner<TMessage>` | direct nested calls to `CommandService` |

### `Pipeline<TMessage, TScope>`

`Execute` is protected and not virtual. `RunNext` and the inline runner call it. Each step adds the element that ran to the record.

```csharp
protected async Task<RunRecord> Execute(PipelineItem<TMessage> item)
{
    var record = new RunRecordBuilder(item.Message);
    if (!Validate(item, record, out var reason)) return record.Rejected(reason);   // no other step
    var scope = CreateScope(item);
    try
    {
        await Before(item, scope, record);
        await Handle(item, scope, record);
        await After(item, scope, record);
        return record.Accepted();
    }
    finally { Close(scope); }                                                     // verbs throw after this
}

protected abstract TScope CreateScope(PipelineItem<TMessage> item);
protected virtual bool Validate(PipelineItem<TMessage> item, RunRecordBuilder r, out string reason);   // validators.TryFor(type)
protected virtual Task Before(PipelineItem<TMessage> item, TScope scope, RunRecordBuilder r);          // before.All(type), in order
protected virtual Task Handle(PipelineItem<TMessage> item, TScope scope, RunRecordBuilder r);          // handlers.For(type)
protected virtual Task After(PipelineItem<TMessage> item, TScope scope, RunRecordBuilder r);           // after.All(type), in order
```

A fault goes into the returned task. The scope closes on every path.

### Item list and `SelectNext`

```csharp
public Task<RunRecord> RunNext()
{
    var item = SelectNext(_items);                 // null: nothing to run now
    if (item == null) return Task.FromResult(RunRecord.None);
    _items.Remove(item);                           // before the run: a running item can Add more
    return Execute(item);
}

protected virtual PipelineItem<TMessage> SelectNext(IReadOnlyList<PipelineItem<TMessage>> items)
    => items.Count > 0 ? items[0] : null;          // FIFO; a stack returns the last item
```

An MTG stack overrides `SelectNext` and returns `null` until both players pass priority.

### `PipelineItem<TMessage>` and `RunRecord`

The item gives each message an id and a state. Replace and cancel come later, as new states or fields on the item.

```csharp
public sealed class PipelineItem<TMessage>
{
    public MessageId Id { get; }                   // from IdAllocator<MessageId> at Add or inline run
    public TMessage Message { get; }
    public ItemState State { get; internal set; }  // Listed, Running, Completed, Rejected, Faulted
}
```

The record keeps only what a caller needs now:

| Field | Why now |
|---|---|
| `Status` | The owner loop stops on `None`. |
| `Message` | The caller sees which message ran. |
| `Reason` | `CommandResult.RejectReason` maps to it. Tests check it. |
| `Steps` | Tests check which elements ran, and their order. |

A fault is not a status. It stays in the faulted task. An inline run returns its own record to the scope. The outer record does not hold it. Events are not in the record, because services emit them to `EventStream`.

### Stores and author bases

The pipeline reads four stores. The router spike owns how each store works.

```csharp
public sealed class PipelineStores<TMessage, TScope>
{
    public TypeRouter<IValidator<TMessage>>               Validators { get; }   // zero or one
    public TypeRouter<IHandler<TMessage, TScope>>         Handlers   { get; }   // exactly one
    public IRouteSource<IHook<TMessage, TScope>>          Before     { get; }   // a list
    public IRouteSource<IHook<TMessage, TScope>>          After      { get; }   // a list
}

public abstract class Handler<T, TMessage, TScope> : IHandler<TMessage, TScope> where T : TMessage
{
    public abstract Task Handle(T message, TScope scope);
}
```

`IValidator<TMessage>`, `IHandler<TMessage, TScope>` and `IHook<TMessage, TScope>` implement `IPipelineStep` and `IRouted`. `IHook` also implements `IOrdered`. A concrete pipeline gives named bases, for example `CommandHandler<T> : Handler<T, Command, ICommandScope>`.

### Scope

A scope holds the verbs of one pipeline that are not in DI. `CreateScope` makes one scope for each item. The concrete pipeline defines the verbs, and decides if the scope exposes the inline runner.

```csharp
public interface ICommandScope { Task<RunRecord> Run(Command command); }   // example; Commands feature owns it

sealed class CommandPipeline : Pipeline<Command, ICommandScope>
{
    protected override ICommandScope CreateScope(PipelineItem<Command> item) => new CommandScope(Inline);
}

sealed class CommandScope : PipelineScope, ICommandScope
{
    readonly IInlineRunner<Command> _inline;
    public Task<RunRecord> Run(Command command) { ThrowIfClosed(); return _inline.Run(command); }
}
```

The pipeline implements `IInlineRunner<TMessage>` explicitly, so `Run` is not on its public surface.

### v2: two or more pipelines

Out of v0. A command scope that adds effects, and an effect scope that runs commands, make a wiring cycle. A holder (`PipelineSet`) solves it. Design it after one pipeline runs.

## 6. Architecture

### Call stack

```
owner system: commands.RunNext()
└─ SelectNext → PlayCard item; remove from the list
   └─ Execute(item)
      ├─ Validate                                   validators.TryFor(PlayCard)
      ├─ CreateScope → CommandScope(Inline)
      ├─ Before                                     before.All(PlayCard)
      ├─ Handle → PlayCardHandler.Handle(c, scope)
      │  ├─ ZoneService.Move → ZoneChanged (wire event)
      │  └─ scope.Run(DrawCard) → IInlineRunner<Command>.Run
      │     └─ Execute(item)                       same order; own RunRecord
      ├─ After                                      after.All(PlayCard)
      └─ close scope → RunRecord(Accepted, Steps)
```

### Logic flow

```mermaid
flowchart TD
  A["RunNext"] --> S{"SelectNext returns an item?"}
  S -- no --> N["RunRecord.None"]
  S -- yes --> R["Remove from list"] --> V{"Validate passes?"}
  I["Inline run"] --> V
  V -- no --> RJ["Rejected record"]
  V -- yes --> C["CreateScope"] --> B["Before"] --> H["Handle"] --> AF["After"] --> CL["Close scope"] --> OK["Accepted record"]
  H -- exception --> F["Close scope, fault the task"]
```

## 7. Trade-offs

| Loss | Why it matters | Mitigation |
|---|---|---|
| A game changes policy with a subclass | The game must know the base class. | Few virtual methods; each has a default. |
| `Execute` is fixed | A pipeline cannot run hooks before validation. | Validation is opt-in for each type. |
| Two ways to get dependencies | Verbs come from the scope. Services come from DI. | One rule: call down with the scope. |
| DI can skip the scope | The scope does not protect services. | Rulebook checks PIPE-02 to PIPE-04. |
| `Remove` cannot stop a running item | A counter to a running effect needs another verb. | Cancel on `PipelineItem` comes later. |
| Every handler is async | A sync handler returns `Task.CompletedTask`. Stack traces are longer. | Named bases; one inline-continuation test. |
| One class for each message type | The VS style (`Apply` on the message) is shorter. | DI handlers are testable. |

## 8. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Check kind |
|---|---|---|
| PIPE-01 | A scope interface exposes only the verbs of its pipeline. | `type.memberTypes`, extended with `includeParameters: true` |
| PIPE-02 | An effect handler does not call a Framework service. (v2) | `il.forbiddenCalls` |
| PIPE-03 | An intent handler does not call a Framework service. (v2) | `il.forbiddenCalls` |
| PIPE-04 | A handler does not call a pipeline or the owner system. | `il.forbiddenCalls` |
| PIPE-05 | A handler does not leave the run thread (`Task.Run`, `Task.Delay`, `ConfigureAwait`). | `il.forbiddenCalls`, extended with a `methods[]` filter |

## 9. Migration steps

Each step is one PR. Each step keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Steps 1 and 2 come after the router step that adds `TypeRouter<T>`, and after the record-ids step that adds `MessageId`.

1. **Base pipeline. No behavior change.** Add `Pipeline<,>`, `PipelineItem<>`, `RunRecord`, `IPipelineStep`, `PipelineStores<,>`, `IHandler<,>`, `IValidator<>`, `IHook<,>` and `IInlineRunner<>`. Add `CommandPipeline` with a scope that has no verbs. `CommandService.Execute` calls the inline runner, throws if the task is not complete, and maps `RunRecord` to `CommandResult`. The block bracket stays in `CommandService` until the Blocks step. Proof: `CommandServiceTests` and all acceptance bindings pass unchanged. Add `PipelineTests`: a reject runs no step, the scope closes on a fault, the step order in `Steps`, `SelectNext` FIFO and LIFO, `Remove` of a running item.
2. **Async handlers.** Change `CommandHandler<T>` to `Task Handle(T, ICommandScope)`. Delete `CommandService`. Add PIPE-01, PIPE-04 and PIPE-05. Add the inline-continuation test (decision 4) in EditMode and PlayMode.
3. **Command hooks.** Fill the command `Before` and `After` stores at composition. Proof: a hook picture with one command type.

v2, after v0 runs: the effect pipeline, `IEffectScope`, `ICommandScope.Add(Effect)`, a LIFO `SelectNext`, the holder for two pipelines, and PIPE-02. Proof: the Fireball and Divine Shield pictures. The engine loop, intents and the `Answer` path move to the engine-loop spike.

## 10. Interfaces needed from the other pieces

| Piece | Needs |
|---|---|
| Router (`infra-type-router.md`) | `TypeRouter<T>.For(Type)` and `TryFor(Type, out T)`. `IRouteSource<T>.All(Type)` with a stable order, no allocation. `IRouted`, `IOrdered`. Hook subscription and base-type match. |
| Record ids (`infra-record-ids.md`) | `MessageId` and `IdAllocator<MessageId>`. The id is local and not on the wire. |
| Blocks (`infra-block-wire.md`) | Later. The bracket must wrap `Before`, `Handle` and `After`, and not `Validate`. |
| Requests | The scope forwards `Ask` to `RequestService`. |

## 11. Later

These items are not open for v0. Each has a place in the design.

| Item | Place |
|---|---|
| Replace a message, also with a type change | `PipelineItem` |
| Cancel an item | `PipelineItem.State` |
| Base-type match for validators and hooks | Router |
| More fields in `RunRecord` (inline tree, events) | `RunRecord`, when a use needs them |
| Two or more pipelines | Section 5, v2 |
