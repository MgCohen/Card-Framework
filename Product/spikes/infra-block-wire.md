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
tags: [#spike, #events, #blocks, #groups, #wire, #causality, #infra]
---

# Shared infrastructure — events and blocks

> **Status: proposed.** One of the shared pipeline infrastructure pieces (pipeline, router, record
> ids, kind ids, events and blocks). This piece owns the event model: the shape of a wire event,
> how to write, listen and react, how to add an event type, blocks and groups. No code changes yet.

## 1. Summary

The server writes game blocks into one ordered stream. A **block** is one line of the game log. A
**group** is a pair of blocks (`GroupStart`, `GroupEnd`) that brackets other blocks, and groups nest.

1. **Two layers.** `WireEvent` (Framework.Wire) is the transport base: pack, send, stream, handlers. `Block : WireEvent` (Framework.Blocks) is the game-log base. Lobby and membership events stay plain `WireEvent`.
2. **Block id = position.** `BlockId` is the position of the block in the stream. No allocator and no id on the wire.
3. **Block types.** A group is 2 blocks. A change (tag, entity, state) is 1 block. A request and its response are 2 independent blocks.
4. **Links.** Parent comes from nesting and is not on the wire. `TriggerId` (on `GroupStart` and `Response`) points to the block that caused the work. `SourceId` points to the entity that did the work.
5. **Write.** Only server services call `IEventEmitter.Emit`. The client never emits game blocks.
6. **Listen.** `HandlerRegistry.Dispatch` runs two stages: first all **apply handlers** (store writers), then all **observers** (read only). Handlers and observers do not emit.
7. **Groups.** `GroupService.Open` / `Close` are public. `Create` is the helper: open, run the logic, close. A group that opens while another group is open is nested. `Close` throws if a child group is open. A group has no kind.
8. **Requests** are not groups. The group that asks closes with `Waiting`. The response is a root block, in any order. The work continues in a new group with `TriggerId` = the response.

### Decisions

User decisions, 2026-10-31 (all infrastructure pieces):

1. Ids are typed `readonly struct`s around `int`. `EntityId` and `IdAllocator<TId>` are in `Framework.Core`.
2. Shared infrastructure (routers, allocator, `RecordStore`) is in `Framework.Core`. `Framework.Wire` references `Framework.Core`.
3. The `Framework.Blocks` feature is accepted.
4. The async core uses `System.Threading.Tasks.Task`, not UniTask.
5. The parent comes from bracket order. It is not on the wire.
6. An intent opens no group. No `Intent` kind and no reserved slot.
7. Hooks exist only at the command level.

User decisions, 2026-11-01 (this piece):

8. **Scope.** This piece owns the event model. Page name: "Events & Blocks".
9. **Nesting.** A group can open inside another group. A group cannot close while a child group is open.
10. **API.** `Open` and `Close` are public. `Create` is the helper. No `BlockHeader` type.
11. **React** is split. This piece defines what an observer gets. The pipeline piece owns the scheduling.
12. **Lobby events** are out. They stay plain `WireEvent` on their own network channels.
13. **Names.** The base class of a game-log line is `Block` (from the Hearthstone block protocol). The bracket pair is a `Group`. "Entry" and "Event" are too generic.
14. **`Block : WireEvent`.** `WireEvent` stays the transport base, because other wire events are not part of the block protocol.
15. **Links.** `Cause` is renamed `Trigger`. Parent, trigger and source are 3 separate values. Parent is derived from nesting. Trigger and parent can agree; both stay.
16. **Block id.** `BlockId` is the position of the block in the stream. A group id is the id of its `GroupStart`.
17. **Groups or not.** The system or the element type decides if work opens a group. Reactions that write more than one block use a group.
18. **Timing.** Nesting follows timing, and the pipeline decides the timing. Default for now: reactions run at the end of the trigger group, before its `Close`.
19. **Requests.** A request is not a group and does not follow the close rule. A response can come after the asking group closes.

User decisions, 2026-11-01 (open questions):

20. **`RequestId` goes away.** A request is a block, so its id is its `BlockId`.
21. **Blocks references.** `Block` stays in `Framework.Blocks`. Tags, Entities, States and Requests reference Blocks.
22. **Root blocks** are permitted. The engine does not open a `Match` group.
23. **No group kind.** No runtime code reads the kind today. `GroupStart` holds `TriggerId` and `SourceId` only. The `Command` / `Effect` kind and the kind registry are removed.
24. **Error path.** If the logic throws, `Create` closes the open child groups as `Errored`, then the parent, and then rethrows the original error.
25. **Deferred:** hidden information (§10 Q1), "what ran" for presentation (§10 Q2), and the response queue (§10 Q3).
26. **Response rule.** A response is always a root block.

Pipeline v0 has no groups and no hooks (Pipeline decision, 2026-11-01). Reaction timing (decision 18) goes to the hooks and reactions discussion. The split at `Ask` (§4.8) waits until groups come into the Pipeline.

## 2. Scope

| Concern | In / out | Owner if out |
|---|---|---|
| `Block` base, block types | in | — |
| Write a block (`Emit`) | in | — |
| Listen: apply stage, observe stage, order | in | — |
| Add a new block type | in | — |
| Groups: `Open` / `Close` / `Create`, nesting rule, `GroupStore` | in | — |
| Links: parent, `TriggerId`, `SourceId` | in | — |
| Request and response as blocks | in: shape and links | request store: `Framework.Requests` |
| React to a block | in: observer contract only | scheduling, timing, hooks: `infra-command-pipeline.md` |
| Find handlers by type | out | `infra-type-router.md` |
| Id struct shape, `RecordStore` base | out | `infra-record-ids.md` |
| Undo, replay cursor | out | `infra-record-ids.md` |
| `WireEvent` transport, packing, channels | out | `Framework.Wire` network (no change) |
| Lobby and membership events | out | `Framework.Multiplayer` |
| Hidden information | out (deferred) | not designed |
| "What ran" for presentation | out (deferred) | a handler for that type, later |

**Boundary.** The piece receives blocks from server services (tags, entities, states, requests,
pipeline). It gives an ordered, well-nested stream to stores, client tools and the network. It
gives the "react" contract to the pipeline, which schedules the work.

## 3. Today

| # | Use | What it does | Location |
|---|---|---|---|
| 1 | Services emit | Each service calls `IEventEmitter.Emit` with its own event. | `Tags/Runtime/TagService.cs:28,38,50`; `Entities/Runtime/EntityService.cs:20`; `States/Runtime/StateService.cs:15`; `Requests/Runtime/RequestService.cs:24` |
| 2 | Apply handlers | `HandlerRegistry` subscribes to `OnAppended` and calls `Apply` for each handler of the type. `Unapply` throws. | `Wire/Runtime/Stream/HandlerRegistry.cs:19,22-26`; `WireEventHandler.cs:11` |
| 3 | Raw listeners | `WireForwarder` and tests subscribe to `EventStream.OnAppended`. Order against handlers is the subscribe order. | `Wire/Runtime/WireForwarder.cs:6`; `Commands/Tests/CommandServiceTests.cs:22` |
| 4 | Client input | `NetworkInboundBridge` emits received events into the client stream. | `Wire/Runtime/NetworkInboundBridge.cs:6` |
| 5 | One base for all | Game events and lobby events derive from the empty `WireEvent`. | `Wire/Runtime/WireEvent.cs:6`; `Multiplayer/Runtime/Lobby/*Event.cs` |
| 6 | New type recipe | Event class, handler, `RegisterHandler<T>()` in an installer. `WireRegistrar` finds the type. | `Bootstrap/Runtime/HandlerRegistrationExtensions.cs:10-12`; `TagInstaller.cs:18-20` |
| 7 | Flat bracket | `BlockScope.Open` / `Close`. One counter. `ParentId` is always 0. | `Wire/Runtime/Events/BlockScope.cs:7-20` |
| 8 | Framework bracket user | `CommandService.Execute`: open, try, catch → `Errored`, close. | `Commands/Runtime/CommandService.cs:28-38` |
| 9 | Bracket events | Packed. `Kind` is a `byte`. Closed `BlockKind` enum with `Trigger`. | `Wire/Runtime/Events/BlockStartEvent.cs:5-11`, `BlockEndEvent.cs:5-9`, `BlockKind.cs:3-9` |
| 10 | Request links | `Request` has its own `Id` and `ParentId`. `Response.RequestId` links back. | `Requests/Runtime/Request.cs:8-9`; `Response.cs:8` |
| 11 | VS nested bracket | Block stack + `parentOverride` for deferred work. | `Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:45-54` |
| 12 | VS bracket copies | The same open, try, finally, close body three times. | VS `CommandService.cs:59-84`, `EffectService.cs:92-111`, `RequestsService.cs:43-56` |
| 13 | VS deferred cause | After hooks capture `CurrentBlockId` into `StackFrame.ParentBlockId`. | VS `CommandService.cs:95,101`; `StackFrame.cs:30`; `Stack.cs:93` |
| 14 | VS source capture | Reflection: property named `Source`, all public properties into a dictionary. | VS `PayloadCapture.cs:16,37-45` |
| 15 | VS readers | `MirrorWorld` skips brackets. The log view indents with a stack. | VS `Mirror/MirrorWorld.cs:60`; `Unity/SliceBootstrap.cs:385-406` |

Paths in rows 1–10 start at `Assets/Framework/`.

### What repeats

| Common part | Seen in | Varies between uses |
|---|---|---|
| Emit one event | #1, #7 | event type |
| Listen to the stream | #2, #3 | path: handler, raw hook |
| Open, run, close a bracket | #8, #12 | kind, parent rule, payload |
| Parent of an event | #7, #10, #11 | always 0, own field, override |
| Id of a line | #7, #10, #11 | own counter in each feature |
| Read the source | #7, #14 | `int` argument, reflection |

## 4. Proposal

### 4.1 API

| Audience | Entry point | Does |
|---|---|---|
| author | `IEventEmitter.Emit(block)` | Writes one block (server services only) |
| author | `WireEventHandler<T>` | Applies the block to a store (both sides) |
| author | `WireEventObserver<T>` | Reads the block after all apply handlers |
| author | `GroupService.Open` / `Close` / `Create` | Brackets work in a group |
| composition | `builder.RegisterHandler<T>()` / `RegisterObserver<T>()` | Adds a handler or an observer |
| composition | `BlocksInstaller` | Store and handlers (both sides); service (server) |
| runtime | `IReadOnlyGroupStore` | Current group, open groups, records |

Shape:

```csharp
// Framework.Wire (transport)
public abstract partial class WireEvent { public int Position { get; internal set; } } // set by EventStream.Add
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

// Framework.Blocks (game log)
public abstract partial class Block : WireEvent
{
    public BlockId Id => new BlockId(Position);  // not packed
}

// Framework.Blocks (server)
public sealed class GroupService
{
    public BlockId Current { get; }
    public BlockId Open(EntityId source = default, BlockId trigger = default);
    public void    Close(BlockId id, GroupOutcome outcome = GroupOutcome.Completed);
    public void    Create(EntityId source, Action logic, BlockId trigger = default);
    public T       Create<T>(EntityId source, Func<T> logic, BlockId trigger = default);
    public Task    CreateAsync(EntityId source, Func<Task> logic, BlockId trigger = default);
    public Task<T> CreateAsync<T>(EntityId source, Func<Task<T>> logic, BlockId trigger = default);
}
```

`trigger = BlockId.None` means "no trigger". `source = EntityId.None` means "no source".

Call site (an effect, after the pipeline port):

```csharp
groups.Create(effect.SourceId, () =>
{
    tags.Set(target, Health, hp - damage);       // TagChange block: inside the effect group
}, trigger: damageBlock);
```

Access:

| Bypass route | What stops it |
|---|---|
| Construct `GroupStart` or `GroupEnd` outside `GroupService` | BLK-01 (`il.callersOnly`) |
| Close a parent while a child is open | `Close` throws |
| Emit from an apply handler or an observer | EVT-01 (`il.forbiddenCalls` on `Emit`) |
| Subscribe to raw `EventStream.OnAppended` | `OnAppended` becomes `internal` |
| Send a block with `Network.Send` | EVT-02: only `Framework.Multiplayer` and `Wire` call `Send` |
| Set `Position` outside `EventStream` | `internal set` |
| Write a store outside a handler | ARCH-05 (existing) |

### 4.2 Block model

```
stream (one ordered list of blocks)

#1  GroupStart  source: Goblin       (play card)
#2    TagChange  player Mana 5 → 3
#3    CardMove   Goblin hand → board
#4  GroupEnd   Completed
#5  GroupStart  source: Knight       (attack)
#6    TagChange  Goblin Health 2 → 0
#7    TagChange  Knight Health 4 → 3
#8    GroupStart  source: Goblin     (death)  trigger: #6
#9      TagSet     Goblin Dead = true
#10     CardMove   Goblin board → graveyard
#11   GroupEnd   Completed
#12 GroupEnd   Completed
```

The number is the `BlockId`. The text in parentheses is a comment, not a field. Group `#8` has parent `#5` (from nesting), trigger `#6` and source Goblin.

Block types:

| Type | Blocks | Links | Close rule |
|---|---|---|---|
| Group | `GroupStart` + `GroupEnd` | `TriggerId`, `SourceId` on `GroupStart` | inner groups close first |
| Change | 1 (`TagSet`, `TagChange`, `EntityCreated`, …) | none | — |
| Request | `Request` + `Response` | `Response.TriggerId` = the request | none: any order |

Rules:

1. A block belongs to the innermost open group. The block holds no parent field.
2. A block with no open group is a root block (for example match setup). A response is always a root block.
3. A group contains blocks and other groups.
4. A group cannot close while a child group is open.
5. Only `GroupStart` and `Response` hold `TriggerId`. Change blocks hold no link fields.
6. Apply handlers and observers do not emit. A reaction schedules work in the pipeline.

When a group is needed: the system or the element type decides. A command or an effect opens a
group. A reaction that writes more than one block opens a group. One change inside a running group
does not open a group.

Dispatch order for one block:

```
Emit(block) → EventStream.Add(block)          Position = next position
  → HandlerRegistry.Dispatch(block)
      1. Apply: every WireEventHandler<T> for the type (registration order)
      2. Observe: every WireEventObserver<T> for the type (registration order)
  → WireForwarder → Network.Send (server only)
```

For `GroupStart`, the apply stage pushes the group, so an observer sees the new group as `Current`.
For `GroupEnd`, the apply stage pops the group, so an observer sees the parent as `Current`.

New block type, one recipe:

1. Add `public partial class XBlock : Block` with `[Packed]` fields in the feature `Runtime/Blocks/`.
2. Add `XHandler : WireEventHandler<XBlock>` if the block changes a store.
3. Add `builder.RegisterHandler<XHandler>()` in the feature installer (both roles).
4. Emit the block from the feature service. `WireRegistrar` finds the type.

### 4.3 Wire shape and ids

```csharp
namespace Framework.Blocks
{
    public partial class GroupStart : Block
    {
        [Packed] public BlockId  TriggerId;  // BlockId.None = no trigger
        [Packed] public EntityId SourceId;   // EntityId.None = no source
    }

    public partial class GroupEnd : Block
    {
        [Packed] public byte Outcome;        // closes the innermost open group
    }

    public enum GroupOutcome : byte { None = 0, Completed = 1, Errored = 2, Cancelled = 3, Waiting = 4 }
}

namespace Framework.Requests
{
    public abstract partial class Request  : Block { [Packed] public EntityId ResponderId; /* … */ }
    public abstract partial class Response : Block { [Packed] public BlockId  TriggerId; }   // the request
}
```

`BlockId` is the position of the block in the stream. `EventStream.Add` sets `Position` on both
sides. Positions start at `1`. `BlockId.None` is `-1` and means "no block". `0` is the default value and means "never set" (a bug). No id field is on the
wire, and no allocator exists.

**Requirement:** the server and the client append the same blocks in the same order. Lobby events
stay off the game stream. If a block is ever filtered for one client, the positions differ (see §10).

`Request.Id`, `Request.ParentId` and `Response.RequestId` are removed. The request id is its
`BlockId`. `RequestId` goes away (record ids piece).

### 4.4 No kind

`GroupStart` holds no kind. No runtime code reads the kind today: only tests assert it, and the VS
tools read the message name. A reaction is a group with a `TriggerId`.

If presentation must know what ran (Attack, Fireball), it adds a handler or an observer for that
type, as the tag listeners do (deferred, §10 Q2).

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
public readonly struct GroupRecord : IRecord<BlockId>
{
    public BlockId Id { get; }                     // the GroupStart position
    public readonly BlockId  ParentId;             // derived: the group open at GroupStart
    public readonly BlockId  TriggerId;
    public readonly EntityId SourceId;
    public readonly int      Depth;
    public readonly GroupOutcome Outcome;          // None while open
}

public interface IReadOnlyGroupStore
{
    BlockId Current { get; }                       // None when no group is open
    IReadOnlyList<BlockId> Open { get; }           // outermost first
    GroupRecord ById(BlockId id);
    bool TryGetById(BlockId id, out GroupRecord record);
}

public sealed class GroupStore : RecordStore<GroupRecord, BlockId>, IReadOnlyGroupStore
{
    internal void Start(GroupStart block);         // ParentId = Current, Write, push
    internal void End(GroupEnd block);             // set Outcome on Current, pop
}

public sealed class GroupStartHandler : WireEventHandler<GroupStart> { /* Apply → Start */ }
public sealed class GroupEndHandler   : WireEventHandler<GroupEnd>   { /* Apply → End   */ }
```

`RecordStore<TRecord, TId>` (record ids piece) gives `ById` and `TryGetById`. Its writes
(`Write`, `Erase`) are protected. `GroupStore` adds the internal writers for the apply handlers.
The handlers also implement `Unapply` when the record ids piece adds undo.

### 4.7 Service (server only)

```csharp
public BlockId Open(EntityId source = default, BlockId trigger = default)
{
    var start = new GroupStart { TriggerId = trigger, SourceId = source };
    _events.Emit(start);
    return start.Id;                               // Position was set by EventStream.Add
}

public void Close(BlockId id, GroupOutcome outcome = GroupOutcome.Completed)
{
    if (!_store.Current.Equals(id))
        throw new InvalidOperationException($"Group {id} has an open child group, or is not open.");
    _events.Emit(new GroupEnd { Outcome = (byte)outcome });
}

public void Create(EntityId source, Action logic, BlockId trigger = default)
{
    var id = Open(source, trigger);
    try   { logic(); }
    catch (OperationCanceledException) { CloseFrom(id, GroupOutcome.Cancelled); throw; }
    catch { CloseFrom(id, GroupOutcome.Errored); throw; }
    Close(id, GroupOutcome.Completed);
}

// Error path only: close the open child groups as Errored, then the group itself.
void CloseFrom(BlockId id, GroupOutcome outcome)
{
    while (!_store.Current.Equals(id))
        _events.Emit(new GroupEnd { Outcome = (byte)GroupOutcome.Errored });
    _events.Emit(new GroupEnd { Outcome = (byte)outcome });
}
```

The other `Create` overloads have the same body. The async ones `await` the logic.

A child group stays open on the error path only if the logic opened it with `Open` and threw before
its `Close`. `CloseFrom` keeps the stream well nested, and the caller sees the original error.

`Close` reads `_store.Current` after earlier emits. This works because `Emit` dispatches to the
apply handlers before it returns. **Requirement:** server dispatch stays synchronous.

### 4.8 Requests

```
#1  GroupStart  source: Fireball     (play card)
#2    TagChange  player Mana 5 → 1
#3    Request    player A: choose target
#4  GroupEnd   Waiting
#5  Response   target = Goblin          trigger: #3     root block
#6  GroupStart  source: Fireball     trigger: #5
#7    TagChange  Goblin Health 2 → 0
#8  GroupEnd   Completed
```

1. The request is a block inside the asking group.
2. The asking group closes with `Waiting`. Other groups can run while the player answers.
3. The response is a root block. Responses to different requests can come in any order.
4. The work continues in a new group with `TriggerId` = the response.

The author still writes `await Ask(...)`. The pipeline closes the group at `Ask` and opens the new
group when the response arrives. If a group is open when the response arrives, the pipeline waits
until no group is open (rule 2).

**Later.** Pipeline v0 has no groups. The split at `Ask` and the response queue come when groups
come into the Pipeline.

### 4.9 Decisions behind the contract

- **Two base classes.** `WireEvent` is transport. `Block` is meaning. Wire does not know blocks. Blocks uses Wire to carry its lines.
- **Position as id.** Every block already has a unique, ordered position on both sides. An allocator and an id field add bytes and a second source of truth.
- **Public `Open` / `Close`.** Authors can see and control the brackets. `Create` is the safe default: it maps the result of the logic to the outcome. Validation stays before `Open`, so a reject emits nothing.
- **Async.** `CreateAsync` keeps the group open across `await`. If two flows open groups at the same time, a close can find an open child and throw. Requests avoid this: the group closes at `Ask`.
- **Parent, trigger, source.** Parent answers "where did it run", trigger "why", source "who". They often differ: in the example in §4.2, group `#8` has 3 different values.
- **Two listen stages.** Observers see the state after all apply handlers. Without the stage split, the order depends on registration order.
- **No emit while dispatch runs.** An emit inside a handler starts a second dispatch before the first one ends. Later handlers of the first block then see the state after the second block.
- **No kind.** A kind field with no reader adds a registry and wire bytes. A field is easy to add later, because no saved data exists yet.
- **No payload.** `wire-block-events.md` L1 holds: change blocks carry the truth. AutoPacker packs only unmanaged fields.
- **Own feature.** The client needs the store and handlers. The pipeline is server only. `Framework.Blocks` lets presentation use groups without the pipeline.

## 5. Call-site migration

| Current code | New code | Change |
|---|---|---|
| `Framework.Wire.BlockScope` | `Framework.Blocks.GroupService` | Rename and move. Add the store. No allocator. |
| `BlockStartEvent`, `BlockEndEvent` | `GroupStart`, `GroupEnd` | Move to Blocks. Drop `Id`, `ParentId`. Add `TriggerId`. |
| `BlockKind` enum, `BlockOutcome` | removed; `GroupOutcome` (+ `Waiting`) | Tests stop asserting the kind. |
| `CommandService.Execute` open / try / catch / close | `_groups.Create(SourceOf(cmd), () => handler.Handle(cmd))` | Validation stays above it. |
| `CommandInstaller`: `Register<BlockScope>` | `BlocksInstaller` in `GameplayLayer`, before `CommandInstaller` | New installer. |
| `TagSetEvent<T>`, `TagChangeEvent<T>`, `EntityCreatedEvent`, `StatePushEvent`, … | Derive from `Block` | Base class change only. |
| `Request.Id`, `Request.ParentId`, `Response.RequestId` | `Request.Id` (position), `Response.TriggerId` | `RequestStore` keys on `BlockId`. |
| `EventStream.Add` | Sets `WireEvent.Position` | One line. |
| `HandlerRegistry` | Two stages: apply, then observe | Add `IWireEventObserver` list. |
| Tests on `OnAppended` | `RecordingObserver` in `Framework.Testing.Support` | `OnAppended` becomes `internal`. |
| `WireForwarder` on `OnAppended` | No change (inside `Framework.Wire`) | — |
| `RoundTripTests` / `NgoTransportPlayTests` use bracket events | `PingEvent` / `PongEvent` | Wire tests stop using domain events. |
| VS `World.OpenBlock(parentOverride)` | `groups.Open(source, trigger: frame.TriggerId)` | Pattern for phase 4. |
| VS `EffectService.RunInline` | `groups.Create(effect.SourceId, () => effect.Run(scope))` | Pattern for phase 4. |
| VS `RequestsService.OpenRequestInternal` | No group. The request block is inside the asking group. | Pattern for phase 4. |
| VS `PayloadCapture` | `ISourced`; no payload on the wire | Dropped. |

## 6. What stays specialized

- **Validation** stays at the call site. A reject must emit nothing.
- **Scheduling and timing** stay in the pipeline. This piece gives `Current` and accepts `trigger`.
- **Lobby events** stay plain `WireEvent` on their own network channels.
- **`GroupOutcome`** stays a closed enum: a group completes, fails, is cancelled or waits.
- **The replay cursor** stays in Wire, owned by the record ids piece.
- **Presentation** registers its own `WireEventObserver<GroupStart>`. No new callback API.

## 7. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Check | Note |
|---|---|---|---|
| BLK-01 | Only `GroupService` constructs `GroupStart` or `GroupEnd`. | `il.callersOnly` | Needs a `type` parameter. |
| BLK-02 | `Framework.Wire` declares no `WireEvent` subtypes. | ARCH-09 `allowed: []` | Replaces the `Block*` exception. |
| BLK-03 | Groups are well nested: each `GroupEnd` closes the innermost open group. | `attached` (test) | Walk the stream with a stack. |
| BLK-04 | A deferred reaction group carries its trigger block as `TriggerId`. | `attached` | Phase 4. |
| BLK-05 | Game-log types derive from `Block`, not directly from `WireEvent`. | `il` (base type) | Lobby and test fixtures excluded. |
| EVT-01 | `WireEventHandler<T>` and `WireEventObserver<T>` subclasses do not call `IEventEmitter.Emit`. | `il.forbiddenCalls` (new) | |
| EVT-02 | Only `Framework.Wire` and `Framework.Multiplayer` call `Network.Send`. | `il.callersOnly` | |

ARCH-02 rows (changed rows only):

```json
"Framework.Wire":      ["Framework.Core"],
"Framework.Blocks":    ["Framework.Core", "Framework.Wire"],
"Framework.Tags":      ["Framework.Core", "Framework.Wire", "Framework.Blocks"],
"Framework.Entities":  ["Framework.Core", "Framework.Tags", "Framework.Wire", "Framework.Blocks"],
"Framework.Requests":  ["Framework.Core", "Framework.Wire", "Framework.Blocks"],
"Framework.States":    ["Framework.Core", "Framework.Wire", "Framework.Blocks"],
"Framework.Commands":  ["Framework.Core", "Framework.Wire", "Framework.Blocks"],
"Framework.Engine":    ["Framework.Wire", "Framework.Blocks", "Framework.Tags", "Framework.Entities",
                        "Framework.Requests", "Framework.States", "Framework.Commands"],
"Framework.Bootstrap": ["Framework.Core", "Framework.Blocks", "Framework.Commands", "Framework.Engine",
                        "Framework.Entities", "Framework.Requests", "Framework.States", "Framework.Tags",
                        "Framework.Wire"]
```

Every feature that writes game-log blocks now references `Framework.Blocks`, because `Block` lives there.

## 8. Migration steps

Each step is one PR and keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Add `-With playmode` to steps 2 to 6.

1. **Rulebook table.** Apply the ARCH-02 rows. Get user approval first.
2. **Two listen stages.** Add `WireEventObserver<T>`, `IWireEventObserver` and `RegisterObserver<T>()`. `HandlerRegistry.Dispatch` runs apply, then observe. Add `RecordingObserver` to `Framework.Testing.Support`. Make `OnAppended` internal.
3. **`Block` base and position.** Create `Framework.Blocks` with `Block` and `BlockId`. `EventStream.Add` sets `Position`. Unit test: positions start at 1 and match on server and client.
4. **Groups.** Add `GroupStart`, `GroupEnd`, `GroupRecord`, `IReadOnlyGroupStore`, `GroupStore`, both handlers and `BlocksInstaller`. Delete the `Block*` events from Wire. Switch the Wire tests to `PingEvent` / `PongEvent`. Tighten ARCH-09 (BLK-02). Unit tests: nested groups give the correct `ParentId` and `Depth`.
5. **Change blocks.** Derive the tag, entity and state events from `Block`.
6. **Requests.** Derive `Request` and `Response` from `Block`. Remove `Request.Id`, `Request.ParentId` and `Response.RequestId`. Add `Response.TriggerId`. Add `GroupOutcome.Waiting`.
7. **`GroupService`.** Replace `BlockScope`. Delete the `BlockKind` enum. Port `CommandService.Execute` to `Create`. Tests: nested `Create` → inner `ParentId` = outer `Id`; throw → `Errored`; cancel → `Cancelled`; close of a parent with an open child throws; throw with an open manual child → both close as `Errored`, and the original error reaches the caller.
8. **Source.** Add `ISourced`.
9. **Rules and docs.** Add BLK-01, BLK-03, BLK-05, EVT-01, EVT-02 (BLK-04 stays `proposed`). Remove the `Block*` exception from `docs/architecture.md`. Add `Blocks/` to the codemap. Describe the block recipe.

## 9. Interfaces with the other pieces

**Needs from the record ids piece:**

- `BlockId` shape (`None`, equality, unmanaged layout). No allocator for blocks.
- `RecordStore<TRecord, TId>` and `IRecord<TId>` for `GroupStore`.
- `EntityId`, and `Unapply` support.
- `RequestId` goes away: a request is identified by its `BlockId`.

**Needs from the router piece:** `TypeMultiRouter<T>` for the apply list and the observe list in `HandlerRegistry`.

**Gives to the pipeline piece:**

- `GroupService.Open` / `Close` / `Create` / `CreateAsync`. No `BlockHeader` type.
- `GroupService.Current` and `trigger`, to link deferred work.
- `GroupOutcome.Waiting`, for the close at `Ask` (later).
- `WireEventObserver<T>` + `IReadOnlyGroupStore.Current`: the contract for reactions.
- `ISourced`.

**Needs from the pipeline piece:**

- Later (not Pipeline v0): timing of reactions, in the hooks and reactions discussion. Default: at the end of the trigger group, before its `Close`.
- Later (not Pipeline v0): close the asking group at `Ask`; queue a response until no group is open; open a new group with `TriggerId` = the response.
- Validation before `Open`.
- A reaction schedules work. It does not emit inside an observer.
- An `Answer` intent opens no group.

**Gives to client tools:** `IReadOnlyGroupStore`, observers, and a well-nested stream.

## 10. Open questions

All open questions are deferred (decision 25, and Q4 until Record Ids is ready).

1. **Hidden information.** If the server sends a block to only one client, the positions differ, and a `TriggerId` points to a different block on each side. Options: a placeholder block (`Hidden`) at the same position; an id field on every block; or redacted fields in the same block. Decide with the hidden-information design.
2. **What ran.** Presentation must know which command or effect ran (Attack, Fireball), not only the source. A handler or an observer for that type gives it. Decide when presentation needs it.
3. **Response queue.** The mechanism that keeps a response a root block (rule 2). Decide with the split at `Ask`.
4. **Default arguments.** `trigger = default` gives `0` ("never set"), not `BlockId.None` (`-1`). Options: `BlockId? trigger = null`, overloads, or reference fields (`Block trigger = null`) when AutoPacker packs records as their key. Decide when the Record Ids shape is ready.
