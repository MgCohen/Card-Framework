---
type: spike
status: proposed
created: 2026-10-31
updated: 2026-11-01
references:
  - Product/spikes/framework-roadmap.md
  - Product/spikes/wire-block-events.md
  - Product/spikes/wire-network-protocol.md
  - Product/spikes/command-service.md
  - Product/spikes/infra-command-pipeline.md
  - Product/spikes/infra-type-router.md
  - Product/spikes/infra-record-ids.md
  - docs/architecture.md
sandbox: Assets/Framework/Blocks/
tags: [#spike, #events, #blocks, #wire, #causality, #infra]
---

# Shared infrastructure — events and blocks

> **Status: proposed.** One of the shared pipeline infrastructure pieces (pipeline, router, record
> ids, kind ids, events and blocks). This piece owns the event model: the shape of a wire event,
> how to write, listen and react, how to add an event type, and blocks. No code changes yet.

## 1. Summary

The server writes wire events into one ordered stream. Blocks are pairs of bracket events in that
stream, and they nest. Each event has one write path, two listen stages and one recipe for a new type.

1. **Event roles.** A wire event is a **bracket** event (`BlockStart`, `BlockEnd`), a **state** event (tags, entities, states) or a **signal** event (`Request`, `Response`). No event holds a parent field: its position in the stream gives its block.
2. **Write.** Only server services call `IEventEmitter.Emit`. The client never emits game events.
3. **Listen.** `HandlerRegistry.Dispatch` runs two stages for each event: first all **apply handlers** (`WireEventHandler<T>`, the store writers), then all **observers** (`WireEventObserver<T>`, read only).
4. **React.** An observer never emits. A server reaction schedules work through the pipeline, with `cause = IReadOnlyBlockStore.Current`.
5. **Blocks.** `BlockService.Open` / `Close` are public. `Create` is a helper: open, run the logic, close. Blocks nest. `Close` throws if the block has an open child block.
6. **Wire shape.** The wire carries `CauseId` only. A `BlockStore` on both sides rebuilds the parent from bracket order. `BlockKind` is an open `TypeKey` set.
7. **Feature.** All `Block*` types move from `Framework.Wire` to a new `Framework.Blocks` feature.

### Decisions

User decisions, 2026-10-31 (all infrastructure pieces):

1. Ids are typed `readonly struct`s around `int`. `BlockId` is in `Framework.Blocks`. `EntityId` and `IdAllocator<TId>` are in `Framework.Core`.
2. Shared infrastructure (routers, allocator) is in `Framework.Core`. `Framework.Wire` references `Framework.Core`.
3. The `Framework.Blocks` feature is accepted.
4. The async core uses `System.Threading.Tasks.Task`, not UniTask.
5. The wire carries `CauseId` only. The parent comes from bracket order.
6. An intent opens no block. No `Intent` kind and no reserved slot.
7. Hooks exist only at the command level.

User decisions, 2026-11-01 (this piece):

8. **Scope.** This piece owns the event model, not only blocks. Page name: "Events & Blocks".
9. **Nesting.** A block can open inside another block. A block cannot close while a child block is open.
10. **API.** `Open` and `Close` are public. `Create` is the helper that does open, logic, close. No `BlockHeader` type: the API takes plain arguments.
11. **React** is split. This piece defines what an observer gets. The pipeline piece owns the scheduling.
12. **Lobby events** (`PeerJoinedEvent`, `MatchStartEvent`, …) are out. They stay on their own network channels.

The `Message` field is dropped from `BlockStartEvent`, because the router gives no slot for each message type. Presentation identifies a block by `Kind` + `SourceId`.

## 2. Scope

| Concern | In / out | Owner if out |
|---|---|---|
| Shape of a wire event, event roles | in | — |
| Write an event (`Emit`) | in | — |
| Listen: apply stage, observe stage, order | in | — |
| Add a new event type | in | — |
| Blocks: `Open` / `Close` / `Create`, nesting rule, `CauseId`, `BlockStore` | in | — |
| Block kinds as an open set | in (uses `TypeKey`) | key design: kind ids piece |
| React to an event | in: observer contract only | scheduling, hooks: `infra-command-pipeline.md` |
| Find handlers by event type | out | `infra-type-router.md` |
| Id types and allocators | out | `infra-record-ids.md` |
| Undo of events, replay cursor | out | `infra-record-ids.md` |
| Network transport, packing, channels | out | `Framework.Wire` network (no change) |
| Lobby and membership events | out | `Framework.Multiplayer` |
| Hidden information (redact `SourceId`) | out | not designed |

**Boundary.** The piece receives events from server services (tags, entities, states, requests,
pipeline). It gives an ordered, well-nested stream to stores, client tools and the network. It
gives the "react" contract to the pipeline, which schedules the work.

## 3. Today

| # | Use | What it does | Location |
|---|---|---|---|
| 1 | Services emit | Each service calls `IEventEmitter.Emit` with its own event. | `Tags/Runtime/TagService.cs:28,38,50`; `Entities/Runtime/EntityService.cs:20`; `States/Runtime/StateService.cs:15`; `Requests/Runtime/RequestService.cs:24` |
| 2 | Apply handlers | `HandlerRegistry` subscribes to `OnAppended` and calls `Apply` for each handler of the type. `Unapply` throws. | `Wire/Runtime/Stream/HandlerRegistry.cs:19,22-26`; `WireEventHandler.cs:11` |
| 3 | Raw listeners | `WireForwarder` and tests subscribe to `EventStream.OnAppended`. Order against handlers is the subscribe order. | `Wire/Runtime/WireForwarder.cs:6`; `Commands/Tests/CommandServiceTests.cs:22` |
| 4 | Client input | `NetworkInboundBridge` emits received events into the client stream. | `Wire/Runtime/NetworkInboundBridge.cs:6` |
| 5 | Lobby send | `Session` calls `Network.Send` directly, not the stream (out of scope). | `Multiplayer/Runtime/Session.cs:227-276` |
| 6 | New type recipe | Event class, handler, `RegisterHandler<T>()` in an installer. `WireRegistrar` finds the type. | `Bootstrap/Runtime/HandlerRegistrationExtensions.cs:10-12`; `TagInstaller.cs:18-20` |
| 7 | Flat bracket | `BlockScope.Open` / `Close`. One counter. `ParentId` is always 0. | `Wire/Runtime/Events/BlockScope.cs:7-20` |
| 8 | Framework bracket user | `CommandService.Execute`: open, try, catch → `Errored`, close. | `Commands/Runtime/CommandService.cs:28-38` |
| 9 | Block events | Packed. `Kind` is a `byte`. Closed `BlockKind` enum with `Trigger`. | `Wire/Runtime/Events/BlockStartEvent.cs:5-11`, `BlockEndEvent.cs:5-9`, `BlockKind.cs:3-9` |
| 10 | Second parent field | `Request.ParentId` is a parent field next to the block parent. | `Requests/Runtime/Request.cs:9` |
| 11 | VS nested bracket | Block stack + `parentOverride` for deferred work. | `Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:45-54` |
| 12 | VS bracket copies | The same open, try, finally, close body three times. | VS `CommandService.cs:59-84`, `EffectService.cs:92-111`, `RequestsService.cs:43-56` |
| 13 | VS deferred cause | After hooks capture `CurrentBlockId` into `StackFrame.ParentBlockId`. | VS `CommandService.cs:95,101`; `StackFrame.cs:30`; `Stack.cs:93` |
| 14 | VS source capture | Reflection: property named `Source`, all public properties into a dictionary. | VS `PayloadCapture.cs:16,37-45` |
| 15 | VS readers | `MirrorWorld` skips blocks. The log view indents with a stack. | VS `Mirror/MirrorWorld.cs:60`; `Unity/SliceBootstrap.cs:385-406` |

Paths in rows 1–10 start at `Assets/Framework/`.

### What repeats

| Common part | Seen in | Varies between uses |
|---|---|---|
| Emit one event | #1, #7 | event type |
| Listen to the stream | #2, #3, #5 | path: handler, raw hook, channel |
| Open, run, close a block | #8, #12 | kind, parent rule, payload |
| Parent of an event | #7, #10, #11 | always 0, stack, override |
| Read the source | #7, #14 | `int` argument, reflection |

## 4. Proposal

### 4.1 API

| Audience | Entry point | Does |
|---|---|---|
| author | `IEventEmitter.Emit(ev)` | Writes one event (server services only) |
| author | `WireEventHandler<T>` | Applies the event to a store (both sides) |
| author | `WireEventObserver<T>` | Reads the event after all apply handlers |
| author | `BlockService.Open` / `Close` / `Create` | Brackets work in a block |
| composition | `builder.RegisterHandler<T>()` / `RegisterObserver<T>()` | Adds a handler or an observer |
| composition | `BlocksInstaller` | Store, handlers, kinds (both sides); service (server) |
| runtime | `IReadOnlyBlockStore` | Current block, open blocks, records |

Shape:

```csharp
// Framework.Wire
public interface IEventEmitter { void Emit(WireEvent ev); }

public abstract class WireEventHandler<T> : IWireEventHandler where T : WireEvent
{
    public abstract void Apply(T ev);            // store writer; must not emit
    public virtual  void Unapply(T ev);          // undo (record ids piece)
}

public abstract class WireEventObserver<T> : IWireEventObserver where T : WireEvent
{
    public abstract void On(T ev);               // runs after all Apply calls; must not emit
}

// Framework.Blocks (server)
public sealed class BlockService
{
    public BlockId Current { get; }
    public BlockId Open(BlockKind kind, EntityId source = default, BlockId cause = default);
    public void    Close(BlockId id, BlockOutcome outcome = BlockOutcome.Completed);
    public void    Create(BlockKind kind, EntityId source, Action logic, BlockId cause = default);
    public T       Create<T>(BlockKind kind, EntityId source, Func<T> logic, BlockId cause = default);
    public Task    CreateAsync(BlockKind kind, EntityId source, Func<Task> logic, BlockId cause = default);
    public Task<T> CreateAsync<T>(BlockKind kind, EntityId source, Func<Task<T>> logic, BlockId cause = default);
}
```

`cause = BlockId.None` means "the cause is the current block". `source = EntityId.None` means "no source".

Call site (an effect level, after the pipeline port):

```csharp
blocks.Create(new EffectBlock(), effect.Source, () =>
{
    tags.Set(target, Health, hp - damage);       // TagChangeEvent: inside the effect block
});
```

Access:

| Bypass route | What stops it |
|---|---|
| Construct `BlockStartEvent` outside `BlockService` | BLK-01 (`il.callersOnly`) |
| Close a parent while a child is open | `Close` throws |
| Emit from an apply handler or an observer | EVT-01 (`il.callersOnly` on `Emit`) |
| Subscribe to raw `EventStream.OnAppended` | `OnAppended` becomes `internal` |
| Write a game event with `Network.Send` | EVT-02: only `Framework.Multiplayer` and `Wire` call `Send` |
| Write a store outside a handler | ARCH-05 (existing) |

### 4.2 Event model

```
stream (one ordered list of wire events)

BlockStart #1 Command  source: card 7            bracket
  TagChange  card 7 Zone Hand → Board            state: in #1
  BlockStart #2 Effect  source: card 7  cause #1  bracket
    TagChange  enemy 3 Health 5 → 3              state: in #2
  BlockEnd   #2 Completed                        bracket
  Request    ChooseTarget                        signal: in #1
BlockEnd   #1 Completed                          bracket
```

Rules:

1. An event belongs to the innermost open block. The event holds no block id.
2. An event with no open block is a root event (for example match setup).
3. A block contains events and other blocks.
4. A block cannot close while a child block is open.
5. Apply handlers read state events and bracket events. Observers read all events.
6. Apply handlers and observers do not emit. A reaction schedules work in the pipeline.

Dispatch order for one event:

```
Emit(ev) → EventStream.Add(ev)
  → HandlerRegistry.Dispatch(ev)
      1. Apply: every WireEventHandler<T> for the type (registration order)
      2. Observe: every WireEventObserver<T> for the type (registration order)
  → WireForwarder → Network.Send (server only)
```

For `BlockStart`, the apply stage pushes the block, so an observer sees the new block as `Current`.
For `BlockEnd`, the apply stage pops the block, so an observer sees the parent as `Current`.

New event type, one recipe:

1. Add `public partial class XEvent : WireEvent` with `[Packed]` fields in the feature `Runtime/Events/`.
2. Add `XHandler : WireEventHandler<XEvent>` if the event changes a store.
3. Add `builder.RegisterHandler<XHandler>()` in the feature installer (both roles).
4. Emit the event from the feature service. `WireRegistrar` finds the type.

### 4.3 Wire events and ids

```csharp
namespace Framework.Blocks
{
    public partial class BlockStartEvent : WireEvent
    {
        [Packed] public BlockId  Id;
        [Packed] public BlockId  CauseId;    // BlockId.None = no cause (root block)
        [Packed] public int      Kind;       // BlockKind slot
        [Packed] public EntityId SourceId;   // EntityId.None = no source
    }

    public partial class BlockEndEvent : WireEvent
    {
        [Packed] public BlockId Id;
        [Packed] public byte    Outcome;
    }

    public enum BlockOutcome : byte { None = 0, Completed = 1, Errored = 2, Cancelled = 3 }
}
```

`BlockId` is a number from one counter. `BlockId.None` (`0`) means "no block". The record ids
piece owns its members. `Request.ParentId` is removed: the position of the request gives its block.

### 4.4 Kinds

```csharp
public abstract record BlockKind(int Slot) : TypeKey(Slot);

public sealed record CommandBlock() : BlockKind(1);
public sealed record EffectBlock()  : BlockKind(2);

public sealed class BlockKindRegistry : SlotRegistry<BlockKind>
{
    public static BlockKindRegistry WithBaselines() { /* Register<CommandBlock>() ... */ }
}
```

This copies `EntityType` + `EntityTypeRegistry`. Games add kinds with `Register<T>()`. Slot 3 (the
old `Trigger`) is not used again. `Trigger` folds into `Effect` + `CauseId`.

### 4.5 Source

```csharp
public interface ISourced
{
    EntityId SourceId { get; }
}
```

A message implements `ISourced` explicitly. The pipeline passes `message is ISourced s ? s.SourceId : EntityId.None` to `Open`. No reflection.

### 4.6 Store and handlers (both roles)

```csharp
public readonly struct BlockRecord
{
    public readonly BlockId  Id;
    public readonly BlockId  ParentId;     // the block that was open when this one started
    public readonly BlockId  CauseId;
    public readonly int      Kind;
    public readonly EntityId SourceId;
    public readonly int      Depth;
    public readonly BlockOutcome Outcome;  // None while open
}

public interface IReadOnlyBlockStore
{
    BlockId Current { get; }                   // None when no block is open
    IReadOnlyList<BlockId> Open { get; }       // outermost first
    BlockRecord ById(BlockId id);
    bool TryById(BlockId id, out BlockRecord record);
}

public sealed class BlockStore : IReadOnlyBlockStore
{
    internal void Insert(BlockStartEvent ev);            // ParentId = Current, then push
    internal void Close(BlockId id, BlockOutcome outcome);
}

public sealed class BlockStartHandler : WireEventHandler<BlockStartEvent> { /* Apply → Insert */ }
public sealed class BlockEndHandler   : WireEventHandler<BlockEndEvent>   { /* Apply → Close  */ }
```

The store keeps all records of the match by id. Open blocks are a stack of ids. The handlers also
implement `Unapply` when the record ids piece adds undo.

### 4.7 Service (server only)

```csharp
public BlockId Open(BlockKind kind, EntityId source = default, BlockId cause = default)
{
    var id = _ids.Next();
    if (cause.IsNone) cause = _store.Current;
    _events.Emit(new BlockStartEvent { Id = id, CauseId = cause, Kind = kind.Slot, SourceId = source });
    return id;
}

public void Close(BlockId id, BlockOutcome outcome = BlockOutcome.Completed)
{
    if (!_store.Current.Equals(id))
        throw new InvalidOperationException($"Block {id} has an open child block, or is not open.");
    _events.Emit(new BlockEndEvent { Id = id, Outcome = (byte)outcome });
}

public void Create(BlockKind kind, EntityId source, Action logic, BlockId cause = default)
{
    var id = Open(kind, source, cause);
    try   { logic(); }
    catch (OperationCanceledException) { Close(id, BlockOutcome.Cancelled); throw; }
    catch { Close(id, BlockOutcome.Errored); throw; }
    Close(id, BlockOutcome.Completed);
}
```

The other `Create` overloads have the same body. The async ones `await` the logic.

`Open` reads `_store.Current` right after an earlier `Emit`. This works because `Emit` dispatches
to `BlockStartHandler` before it returns. **Requirement:** server dispatch stays synchronous.

### 4.8 Decisions behind the contract

- **Public `Open` / `Close`.** The model is "message nesting". Authors can see and control the brackets. `Create` is the safe default: it maps the result of the logic to the outcome. Validation stays before `Open`, so a reject emits nothing.
- **Async.** `CreateAsync` keeps the block open across `await`. If two flows open blocks at the same time, a close can find an open child and throw. The pipeline must not start a second flow while a block waits.
- **One wire field.** The parent is already in the bracket order. The wire carries only `CauseId`. The default cause is the current block. Deferred work passes the block that scheduled it. A reaction is a block with `CauseId != ParentId`.
- **Two listen stages.** Observers see the state after all apply handlers. Without the stage split, the order depends on registration order.
- **No emit while dispatch runs.** An emit inside a handler starts a second dispatch before the first one ends. Later handlers of the first event then see the state after the second event.
- **Open kinds.** `docs/coding-standards.md` says an author-extensible set is not an enum. Games need group blocks (turn, combat phase, SBA batch).
- **No payload.** `wire-block-events.md` L1 holds: leaf events carry the truth. AutoPacker packs only unmanaged fields.
- **Own feature.** The client needs the store and handlers. The pipeline is server only. `Framework.Blocks` lets presentation use blocks without the pipeline.

## 5. Call-site migration

| Current code | New code | Change |
|---|---|---|
| `Framework.Wire.BlockScope` | `Framework.Blocks.BlockService` | Rename and move. Add store and allocator. |
| `CommandService.Execute` open / try / catch / close | `_blocks.Create(new CommandBlock(), SourceOf(cmd), () => handler.Handle(cmd))` | Validation stays above it. |
| `CommandInstaller`: `Register<BlockScope>` | `BlocksInstaller` in `GameplayLayer`, before `CommandInstaller` | New installer. |
| `HandlerRegistry` | Two stages: apply, then observe | Add `IWireEventObserver` list. |
| Tests on `OnAppended` | `RecordingObserver` in `Framework.Testing.Support` | `OnAppended` becomes `internal`. |
| `WireForwarder` on `OnAppended` | No change (inside `Framework.Wire`) | — |
| `Request.ParentId` | Removed | Position gives the block. |
| Tests: `(byte)BlockKind.Command`, `start.ParentId` | `new CommandBlock().Slot`, `start.CauseId` | Mechanical. |
| `RoundTripTests` / `NgoTransportPlayTests` use Block events | `PingEvent` / `PongEvent` | Wire tests stop using domain events. |
| VS `World.OpenBlock(parentOverride)` | `blocks.Open(kind, source, cause: frame.CauseId)` | Pattern for phase 4. |
| VS `EffectService.RunInline` | `blocks.Create(new EffectBlock(), effect.Source, () => effect.Run(scope))` | Pattern for phase 4. |
| VS `RequestsService.OpenRequestInternal` | No block. The request event is inside the open effect block. | Pattern for phase 4. |
| VS `PayloadCapture` | `ISourced`; no payload on the wire | Dropped. |

## 6. What stays specialized

- **Validation** stays at the call site. A reject must emit nothing.
- **Scheduling** stays in the pipeline. This piece gives `Current` and accepts `cause`.
- **Lobby events** stay on their own network channels.
- **`BlockOutcome`** stays a closed enum: a block completes, fails or is cancelled.
- **The replay cursor** stays in Wire, owned by the record ids piece.
- **Presentation** registers its own `WireEventObserver<BlockStartEvent>`. No new callback API.

## 7. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Check | Note |
|---|---|---|---|
| BLK-01 | Only `BlockService` constructs `BlockStartEvent` or `BlockEndEvent`. | `il.callersOnly` | Needs a `type` parameter. |
| BLK-02 | `Framework.Wire` declares no `WireEvent` subtypes. | ARCH-09 `allowed: []` | Replaces the `Block*` exception. |
| BLK-03 | Brackets are well nested: each end closes the innermost open block. | `attached` (test) | Walk the stream with a stack. |
| BLK-04 | A deferred reaction carries the block that scheduled it as `CauseId`. | `attached` | Phase 4. |
| EVT-01 | `WireEventHandler<T>` and `WireEventObserver<T>` subclasses do not call `IEventEmitter.Emit`. | `il.forbiddenCalls` (new) | |
| EVT-02 | Only `Framework.Wire` and `Framework.Multiplayer` call `Network.Send`. | `il.callersOnly` | |

ARCH-02 rows (changed rows only):

```json
"Framework.Wire":      ["Framework.Core"],
"Framework.Blocks":    ["Framework.Core", "Framework.Wire"],
"Framework.Commands":  ["Framework.Core", "Framework.Wire", "Framework.Blocks"],
"Framework.Engine":    ["Framework.Wire", "Framework.Blocks", "Framework.Tags", "Framework.Entities",
                        "Framework.Requests", "Framework.States", "Framework.Commands"],
"Framework.Bootstrap": ["Framework.Core", "Framework.Blocks", "Framework.Commands", "Framework.Engine",
                        "Framework.Entities", "Framework.Requests", "Framework.States", "Framework.Tags",
                        "Framework.Wire"]
```

## 8. Migration steps

Each step is one PR and keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Add `-With playmode` to steps 2, 4 and 5.

1. **Rulebook table.** Apply the ARCH-02 rows. Get user approval first.
2. **Two listen stages.** Add `WireEventObserver<T>`, `IWireEventObserver` and `RegisterObserver<T>()`. `HandlerRegistry.Dispatch` runs apply, then observe. Add `RecordingObserver` to `Framework.Testing.Support`. Make `OnAppended` internal.
3. **Store and handlers.** Create `Framework.Blocks`. Add `BlockRecord`, `IReadOnlyBlockStore`, `BlockStore`, both handlers and `BlocksInstaller`. Unit tests: nested blocks give the correct `ParentId` and `Depth`.
4. **Move the events.** Move the `Block*` events to `Framework.Blocks`. Switch the Wire tests to `PingEvent` / `PongEvent`. Tighten ARCH-09 (BLK-02).
5. **Wire shape.** Needs `EntityId` and `BlockId`. Rename `ParentId` → `CauseId`. Change the field types. Remove `Request.ParentId`.
6. **`BlockService`.** Replace `BlockScope`. Port `CommandService.Execute` to `Create`. Tests: nested `Create` → inner `CauseId` = outer `Id`; throw → `Errored`; cancel → `Cancelled`; close of a parent with an open child throws.
7. **Open kinds.** Replace the `BlockKind` enum with the records and the registry.
8. **Source.** Add `ISourced`.
9. **Rules.** Add BLK-01, BLK-03, EVT-01, EVT-02. BLK-04 stays `proposed`.
10. **Docs.** Remove the `Block*` exception from `docs/architecture.md`. Add `Blocks/` to the codemap. Describe the event recipe.

## 9. Interfaces with the other pieces

**Needs from the record ids piece:** `BlockId` (`Value`, `None`, equality, unmanaged layout), `IdAllocator<BlockId>` (server only, reset for each match), `EntityId`, and `Unapply` support.

**Needs from the router piece:** `TypeMultiRouter<T>` for the apply list and the observe list in `HandlerRegistry`.

**Gives to the pipeline piece:**

- `BlockService.Open` / `Close` / `Create` / `CreateAsync`. No `BlockHeader` type.
- `BlockService.Current`, to capture a cause when work is deferred.
- `WireEventObserver<T>` + `IReadOnlyBlockStore.Current`: the contract for reactions to events.
- `ISourced`.

**Needs from the pipeline piece:**

- One flow at a time while a block waits across an `await`.
- Validation before `Open`.
- A reaction schedules work. It does not emit inside an observer.
- An `Answer` intent opens no block.

**Gives to client tools:** `IReadOnlyBlockStore`, observers, and a well-nested stream.

## 10. Open questions

1. **Root events.** This spike permits events outside a block (rule 2). Does the engine open a root block (`Match`, `Setup`) instead?
2. **Kind slot ranges.** Does the framework reserve a slot range for baseline kinds?
3. **`Trigger` fold.** Does presentation need a separate trigger kind? Decide in the phase 4 reactions spike.
4. **Close check on the error path.** The nesting exception hides the original exception. Log and rethrow the original, or check only in tests?
5. **Hidden information.** Must `SourceId` be redacted for a hidden card?
6. **Message identity.** If presentation needs "which command" without a source, who gives a stable slot?
