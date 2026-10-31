---
type: spike
status: proposed
created: 2026-10-31
references:
  - Product/spikes/framework-roadmap.md
  - Product/spikes/wire-block-events.md
  - Product/spikes/wire-network-protocol.md
  - Product/spikes/command-service.md
  - docs/architecture.md
  - docs/lookup-patterns.md
sandbox: Assets/Framework/Blocks/
tags: [#spike, #blocks, #wire, #causality, #pipeline, #infra]
---

# Shared infrastructure — block and wire system

> **Status: proposed.** This spike is one of four parallel pieces of the shared
> pipeline infrastructure (intent, effect, command, event). The other pieces are
> the type-keyed router, record ids and allocation, and the command pipeline.
> This piece owns block brackets, nesting, causality, and the block events on the
> stream. No code changes yet.

## 1. TL;DR

1. Move all `Block*` types out of `Framework.Wire` into a new `Framework.Blocks` feature. Wire then holds no domain events, with no exception.
2. One server API brackets every level: `blocks.Run(header, body)` and `blocks.RunAsync(header, body)`. There is no public `Open` / `Close`.
3. The wire carries one parent field, `CauseId`. The lexical parent comes from bracket order. A `BlockStore` (both sides) rebuilds it, so nesting is identical on server and client.
4. `BlockKind` becomes an open `TypeKey` record set with a registry. Baselines: `Command`, `Effect`. `Trigger` folds into `Effect` + `CauseId`. Intents and requests open no block.
5. `ISourced` replaces `PayloadCapture`. `BlockStartEvent` carries `Id`, `CauseId` (`BlockId`), `Kind` (`int`), `SourceId` (`EntityId`). No payload goes on the wire.

### Decisions (2026-10-31)

User decisions that all four infrastructure spikes use:

1. Ids are typed `readonly struct`s around `int`. `BlockId` is in `Framework.Blocks`. `EntityId` and `IdAllocator<TId>` are in `Framework.Core`.
2. Shared infrastructure (routers, allocator) is in `Framework.Core`. `Framework.Wire` references `Framework.Core`.
3. The `Framework.Blocks` feature is accepted. §6 gives the ARCH-02 rows.
4. The async core uses `System.Threading.Tasks.Task`, not UniTask.
5. The parent model is accepted: the wire carries `CauseId` only. The nesting parent comes from bracket order.
6. An intent opens no block, so an `Answer` intent can resume a chain that waits on a request without breaking nesting. **This spike removes `Intent` from the baseline kinds. It does not reserve a slot.** The registry can add a kind later, so a reserved slot gives nothing.
7. Hooks exist only at the command level.

Consequence for this spike: the router gives no `int` slot for each message type. **The `Message` field is dropped** from `BlockStartEvent`. Presentation identifies a block by `Kind` + `SourceId` (and the source's definition), as `wire-block-events.md` L1 says. Open question 8 tracks a return of the field.

## 2. Current shapes

| # | Copy | What it does | Location |
|---|---|---|---|
| 1 | `BlockScope` counter | Only block-id counter. Comment warns about collisions. | `Assets/Framework/Wire/Runtime/Events/BlockScope.cs:7-8` |
| 2 | `BlockScope.Open` | Allocates an id, emits `BlockStartEvent`, always writes `ParentId = 0`. | `Assets/Framework/Wire/Runtime/Events/BlockScope.cs:12-17` (`:15`) |
| 3 | `BlockScope.Close` | Emits `BlockEndEvent` with an outcome. | `Assets/Framework/Wire/Runtime/Events/BlockScope.cs:19-20` |
| 4 | `BlockStartEvent` / `BlockEndEvent` | Packed wire events. `Kind` is a `byte`. | `Assets/Framework/Wire/Runtime/Events/BlockStartEvent.cs:5-11`, `BlockEndEvent.cs:5-9` |
| 5 | `BlockKind` (framework) | Closed enum: `None`, `Command`, `Effect`, `Trigger`. | `Assets/Framework/Wire/Runtime/Events/BlockKind.cs:3-9` |
| 6 | Framework `CommandService.Execute` | Open, try, catch → `Errored` + rethrow, close `Completed`. | `Assets/Framework/Commands/Runtime/CommandService.cs:28-38` |
| 7 | `CommandInstaller` | Registers `BlockScope` server-only. | `Assets/Framework/Bootstrap/Runtime/CommandInstaller.cs:17` |
| 8 | Slice `World.OpenBlock` / `CloseBlock` | Allocator + open-block stack. `parentOverride` replaces the stack parent. | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:45-54` |
| 9 | Slice `CommandService.Execute` | Open, reflect payload, try / catch / finally, close. | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/CommandService.cs:59-84` |
| 10 | Slice `EffectService.RunInline` | Same body as #9, `BlockKind.Effect`, validate first. | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/EffectService.cs:84-112` |
| 11 | Slice `RequestsService.OpenRequestInternal` | Bodyless `Request` block around option leaves. | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/RequestsService.cs:35-58` |
| 12 | Slice causal capture | After-phase hooks capture `CurrentBlockId` into `StackFrame.ParentBlockId`; `Stack.ResolveAsync` passes it back. | `CommandService.cs:95,101`; `StackFrame.cs:30`; `Stack.cs:93` |
| 13 | Slice `PayloadCapture` | Reflection: all public properties → dict; property named `Source` → entity. | `Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/PayloadCapture.cs:16,37` |
| 14 | Slice `BlockKind` | Closed enum: `Effect`, `Command`, `Request`. | `Assets/Spike/ECS/VerticalSlice/Engine/Stream/BlockKind.cs:16` |
| 15 | Slice replay | `MirrorWorld.Apply` / `Unapply` (blocks are no-ops); `ScrubController.GoTo` walks a cursor. | `Mirror/MirrorWorld.cs:36,64`; `Mirror/ScrubController.cs:27-41` |
| 16 | Framework dispatch | `HandlerRegistry.Dispatch` calls `Apply` only. `Unapply` throws. | `Assets/Framework/Wire/Runtime/Stream/HandlerRegistry.cs:22`; `WireEventHandler.cs:11` |
| 17 | Block events as test fixtures | Wire tests use Block events as generic payloads. | `Assets/Framework/Wire/Tests/RoundTripTests.cs:16-17,140-216`; `Wire/PlayTests/NgoTransportPlayTests.cs:60-65` |

Three copies (#6, #9, #10) repeat one body. Two copies (#1, #8) own an allocator. Two copies (#2, #8) disagree on nesting.

## 3. Generalized contract

New feature `Framework.Blocks` (Runtime refs: `Framework.Core`, `Framework.Wire`, `Scaffold.Autopacker`). One top-level type per file. Comments only where they warn.

### Wire events

```csharp
namespace Framework.Blocks
{
    public partial class BlockStartEvent : WireEvent
    {
        [Packed] public BlockId  Id;
        [Packed] public BlockId  CauseId;    // BlockId.None = no cause
        [Packed] public int      Kind;       // BlockKind slot
        [Packed] public EntityId SourceId;   // EntityId.None = no source
    }

    public partial class BlockEndEvent : WireEvent
    {
        [Packed] public BlockId Id;
        [Packed] public byte    Outcome;
    }

    public readonly struct BlockId : IEquatable<BlockId>
    {
        public readonly int Value;
        public BlockId(int value) => Value = value;
        public static readonly BlockId None = default;   // 0 is never allocated
        public bool IsNone => Value == 0;
        public bool Equals(BlockId other) => Value == other.Value;
    }

    public enum BlockOutcome : byte { None = 0, Completed = 1, Errored = 2, Cancelled = 3 }
}
```

`BlockId` and `EntityId` are unmanaged structs, so AutoPacker packs them as fields. If the generator rejects a struct field, use `[Packed(typeof(int))]` with an `IPackingHandler` conversion. The ids piece owns the exact `BlockId` members (equality, operators, `ToString`); this spike needs only `Value`, `None` and equality. `Message` is dropped (see Decisions).

`BlockOutcome` stays an enum. It is a closed set: a body completes, fails, or is cancelled.

### Kinds (open set, Registry pattern)

```csharp
public abstract record BlockKind(int Slot) : TypeKey(Slot);

public sealed record CommandBlock() : BlockKind(1);
public sealed record EffectBlock()  : BlockKind(2);

public sealed class BlockKindRegistry : SlotRegistry<BlockKind>
{
    public static BlockKindRegistry WithBaselines() { /* Register<CommandBlock>() ... */ }
}
```

This copies `EntityType` + `EntityTypeRegistry`. Games add kinds with `Register<T>()`. `SlotRegistry` rejects slot clashes. Slot 3 (the old `Trigger`) is not used again. No `Intent` kind exists and no slot is reserved for it (decision 6).

### Source and header

```csharp
public interface ISourced
{
    EntityId SourceId { get; }
}

public readonly struct BlockHeader
{
    public readonly BlockKind Kind;
    public readonly EntityId  SourceId;
    public readonly BlockId   CauseId;   // None = use the lexical parent

    public BlockHeader(BlockKind kind, EntityId sourceId = default, BlockId causeId = default) { ... }

    public static BlockHeader Of(BlockKind kind, object message, BlockId causeId = default)
        => new(kind, message is ISourced s ? s.SourceId : EntityId.None, causeId);
}
```

A message implements `ISourced` explicitly, for example `EntityId ISourced.SourceId => Source;`. The message holds a typed `EntityId Source`, not a live `Entity` (ids spike, MSG-01). Its handler resolves the entity through a read view.

### Store and handlers (both roles)

```csharp
public readonly struct BlockRecord
{
    public readonly BlockId  Id;
    public readonly BlockId  ParentId;     // lexical: the block open when this one started
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
    // Sole writers: BlockStartHandler and BlockEndHandler.
    internal void Insert(BlockStartEvent ev);            // ParentId = Current, then push
    internal void Close(BlockId id, BlockOutcome outcome);
    internal void Remove(BlockId id);                    // Unapply of start
    internal void Reopen(BlockId id);                    // Unapply of end
}

public sealed class BlockStartHandler : WireEventHandler<BlockStartEvent> { /* Apply → Insert; Unapply → Remove */ }
public sealed class BlockEndHandler   : WireEventHandler<BlockEndEvent>   { /* Apply → Close;  Unapply → Reopen */ }
```

The store keeps all records of the match by id. Open blocks are a stack of ids. `Insert` is idempotent on the same id, as the `Store` pattern requires.

### Service (server only)

```csharp
public sealed class BlockService
{
    readonly IEventEmitter _events;
    readonly IReadOnlyBlockStore _store;
    readonly IdAllocator<BlockId> _ids;

    public BlockService(IEventEmitter events, IReadOnlyBlockStore store, IdAllocator<BlockId> ids) { ... }

    public BlockId Current => _store.Current;

    public void Run(in BlockHeader header, Action body);
    public void Run<TState>(in BlockHeader header, TState state, Action<TState> body);
    public T    Run<T>(in BlockHeader header, Func<T> body);
    public Task RunAsync(BlockHeader header, Func<Task> body);
    public Task<T> RunAsync<T>(BlockHeader header, Func<Task<T>> body);
}
```

Every overload has the same body. The async ones `await` in place of the call:

```csharp
public void Run(in BlockHeader header, Action body)
{
    var id = Open(header);
    try   { body(); }
    catch (OperationCanceledException) { Close(id, BlockOutcome.Cancelled); throw; }
    catch { Close(id, BlockOutcome.Errored); throw; }
    Close(id, BlockOutcome.Completed);
}

BlockId Open(in BlockHeader h)
{
    var id    = _ids.Next();
    var cause = h.CauseId.IsNone ? _store.Current : h.CauseId;
    _events.Emit(new BlockStartEvent { Id = id, CauseId = cause, Kind = h.Kind.Slot, SourceId = h.SourceId });
    return id;
}

void Close(BlockId id, BlockOutcome outcome)
{
    // Another flow opened a block during an await: the stream is no longer well nested.
    if (!_store.Current.Equals(id)) throw new InvalidOperationException($"Block {id} is not the innermost open block.");
    _events.Emit(new BlockEndEvent { Id = id, Outcome = (byte)outcome });
}
```

### Decisions behind the contract

- **Q1 — `Run`, not `using`.** A `using` handle cannot see the exception in `Dispose`. It needs a `Complete()` call that authors forget. `Run` maps the result: return → `Completed`, `OperationCanceledException` → `Cancelled`, other exception → `Errored` + rethrow. Validation stays before `Run`, so a reject emits nothing (command-service L1 Phase 1). A bodyless block is `Run(header, () => { })`.
- **Q1 — async.** `RunAsync` uses `System.Threading.Tasks.Task` (decision 4). It keeps the block open across `await`. A command that awaits a `Request` emits the request event inside its own block. The continuation emits inside the same block. Cancellation (`RequestService.CancelAllPending`) closes the block as `Cancelled`. This needs one invariant from the pipeline: **one flow at a time** (§8).
- **Q2 — one wire field, two concepts.** The lexical parent is already in the stream: the bracket order shows it. A second wire field would repeat it. So the wire carries only `CauseId`. The default cause is the lexical parent. Deferred work (the slice `StackFrame.ParentBlockId`) passes its captured id as `CauseId`. `BlockStore` rebuilds `ParentId` from bracket order on both sides. A reaction is a block with `CauseId != ParentId`.
- **Q3 — open kinds.** `docs/coding-standards.md` says an author-extensible set is not an enum. The pipeline levels are framework-defined, but games need grouping blocks (turn, combat phase, SBA batch). `Intent` is not a kind: an intent opens no block (decision 6). An `Answer` intent then resumes the waiting chain inside the block that is still open. `Request` is not a kind: a framework request is a typed leaf event (request-service L2). `Trigger` folds into `Effect` with a cause (§5).
- **Q4 — no payload.** `wire-block-events.md` L1 holds: leaves carry the truth. The slice `Name` string is dropped with no replacement, because the router gives no message slot. Presentation identifies a block by `Kind` + `SourceId`. AutoPacker packs only unmanaged fields, so a reflected dictionary cannot travel.
- **Q5 — own feature.** The architecture invariant allows `Block*` in Wire only "until the command-pipeline feature graduates". The client needs the store and handlers, and the pipeline is server-only. A separate `Framework.Blocks` lets presentation depend on blocks without the pipeline. Decision 3 accepts it.
- **Q6 — replay cursor.** Not built in this piece. This piece makes blocks reversible: both block handlers implement `Unapply`. The cursor is a Wire concern (§5).
- **Q7 — client tools.** See §5 and §8.

## 4. Call-site migration

| Current code | New code | Change needed |
|---|---|---|
| `Framework.Wire.BlockScope` | `Framework.Blocks.BlockService` | Rename and move. Add store + allocator dependencies. Remove public `Open` / `Close`. |
| `CommandService.Execute` open / try / catch / close (`CommandService.cs:28-38`) | `return _blocks.Run(BlockHeader.Of(CommandBlock, cmd), () => { handler.Handle(cmd); return CommandResult.Accept(); });` | Body shrinks to one call. Validation stays above it. |
| `CommandInstaller`: `Register<BlockScope>` | `BlocksInstaller`: `BlockService` server-only; `BlockStore`, both handlers, `BlockKindRegistry` on both roles | New installer in `GameplayLayer`, before `CommandInstaller`. |
| `EngineStack` / `CommandServiceTests`: `new BlockScope(Stream)` | `BlocksFixture.Build(stream)` (store + handlers + service) in `Framework.Testing.Support` | One helper, reused by unit and integration tiers. |
| Tests: `(byte)BlockKind.Command`, `start.ParentId` | `new CommandBlock().Slot`, `start.CauseId` | Mechanical. |
| `RoundTripTests` / `NgoTransportPlayTests` use Block events | Use `PingEvent` / `PongEvent` fixtures; block round-trip tests move to `Framework.Blocks.Tests` | Wire tests stop depending on domain events. |
| Slice `World.OpenBlock(parentOverride)` | `BlockHeader(..., causeId: frame.CauseId)` | Pattern for the phase 4 port. Spike code does not change. |
| Slice `EffectService.RunInline` / `CommandService.Execute` | `blocks.Run(BlockHeader.Of(EffectBlock, effect), () => effect.Run(scope))` | Pattern for phase 4. |
| Slice `RequestsService.OpenRequestInternal` | No block. The typed request event is a leaf inside the open effect block. | Pattern for phase 4. |
| Slice `StackFrame.ParentBlockId` | `StackFrame.CauseId = blocks.Current` at schedule time | Pattern for phase 4. Owned by the pipeline piece. |
| Slice `PayloadCapture.ExtractSource` | `ISourced` on the message | Pattern for phase 4. |
| Slice `PayloadCapture.Of` | Nothing on the wire. A server-side debug log may keep it later. | Dropped. |

## 5. What stays specialized, and why

- **Validation stays at the call site.** Each level validates differently (command validator, effect `Validate`). The block API only brackets. A reject must emit nothing, so validation must run before `Run`.
- **Deferred scheduling stays in the pipeline.** This piece gives `Current` and accepts `CauseId`. The pipeline decides when to capture the cause (After hooks, triggers, request resolution). Hooks exist only at the command level (decision 7). A Before hook runs inside the command block. An After hook is deferred and carries the command block as `CauseId`.
- **`Trigger` is folded, not kept.** A triggered reaction is an effect. Its identity comes from `SourceId` (and the source's definition). "Reactive" comes from `CauseId != ParentId`. A separate kind would split one fact over two fields. If presentation shows that this is not enough, a game or the framework registers a `TriggerBlock` record later; the registry makes that a one-line change.
- **The event level opens no block.** Events are leaves. A reaction to an event opens an `Effect` block with the cause of the block that emitted the event.
- **`BlockOutcome` stays a closed enum.** The outcomes are the three ways a body can end.
- **The replay cursor stays in Wire.** A cursor over `EventStream` + `HandlerRegistry` holds no domain types. It needs: `HandlerRegistry.Undispatch(ev)` (handlers in reverse order), an `Unapply` in every handler, and events that carry their inverse (`TagChangeEvent` already has `Prev`). Blocks need no inverse data: `Remove` and `Reopen` use the record that the store keeps. The cursor is a separate spike when rollback is scheduled (roadmap "Not scheduled").
- **Presentation hooks are plain handlers.** A client tool registers its own `WireEventHandler<BlockStartEvent>`. `HandlerRegistry` already fans out. No new callback API.

What client tools get from this piece:

1. Open and close of each block, with `Kind`, `SourceId`, `CauseId`, `ParentId`, `Depth`, `Outcome`.
2. `IReadOnlyBlockStore.Current` when a leaf arrives. A tool groups the leaf into that block.
3. `ById(CauseId)` after the cause has closed. A tool chains animations ("damage plays after the cast").
4. A guarantee that the stream is well nested. A tool can use a simple stack.

## 6. Rulebook rules to propose

Proposals only. `rulebook.json` does not change in this spike.

| Id | Rule | Check | Note |
|---|---|---|---|
| BLK-01 | Only `BlockService` constructs `BlockStartEvent` or `BlockEndEvent` in Framework runtime code. | `il.callersOnly`, callee `Framework.Blocks.Block*Event` constructors. | `allowedCallers` supports only `subclassOf` today. The check needs a `type` parameter. |
| BLK-02 | `Framework.Wire` declares no `WireEvent` subtypes. | Tighten ARCH-09: `allowed: []`. | Replaces the `Block*` exception. |
| BLK-03 | Block brackets in the stream are well nested: each start has one end, and each end closes the innermost open block. | `attached` (test-backed, like TEST-02). | Test: run a nested command + an async command through the pipeline; walk the stream with a stack. |
| BLK-04 | A deferred reaction carries the id of the block that scheduled it as `CauseId`. | `attached`. | Lights up in phase 4. Status `proposed` until then. |
| BLK-05 | Each `WireEventHandler<T>` overrides `Unapply`. | New IL or reflection check. | Severity `warning` until rollback is scheduled. |
| ARCH-02 edit | See the rows below. | Existing check. | Table change, not a new rule. |

Exact ARCH-02 `allowed` rows to propose (changed rows only; the other rows stay as they are):

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

`Framework.Wire → Core` comes from decision 2 and may also appear in the router and ids spikes. `Framework.Commands → Core` is for `EntityId` in `ISourced`. The pipeline spike may add its own assembly; that assembly then needs `Framework.Blocks` in its row.

The store writer rule is already ARCH-05: `BlockStore` internals are called only from `WireEventHandler<T>` subclasses.

## 7. Migration steps

Each step lands as its own PR and keeps `powershell -NoProfile -File Tools/Rulebook/gate.ps1` green. Add `-With playmode` to steps 3 and 4 (Wire changes).

1. **Rulebook table.** Apply the ARCH-02 rows in §6. Get user approval: this is a rulebook edit. Nothing references the new entry yet, so the gate stays green.
2. **Store and handlers.** Create `Framework.Blocks` (Runtime + Tests). Add `BlockRecord`, `IReadOnlyBlockStore`, `BlockStore`, `BlockStartHandler`, `BlockEndHandler`, against the current Wire events (`ParentId` treated as cause). Add `BlocksInstaller` (both roles). Unit tests: nested brackets give the correct `ParentId` and `Depth`; `Unapply` reverses both events. ARCH-08 needs these tests.
3. **Move the events.** Move `BlockStartEvent`, `BlockEndEvent`, `BlockOutcome`, `BlockKind` to `Framework.Blocks`. Switch `RoundTripTests` and `NgoTransportPlayTests` to `PingEvent` / `PongEvent`. Move block round-trip tests to `Framework.Blocks.Tests`. Tighten ARCH-09 to `allowed: []` (BLK-02). `WireRegistrar` sorts by `FullName`, so channel ids shift on both sides the same way.
4. **Wire shape.** Needs `EntityId` in `Framework.Core` and `BlockId` (ids piece). Rename `ParentId` → `CauseId`. Change `Id` / `CauseId` to `BlockId`, `SourceId` to `EntityId`, `Kind` to `int`. Update the tests in §4.
5. **`BlockService`.** Replace `BlockScope` with `BlockService` (`Run` / `RunAsync`, store-backed `Current`). Inject `IdAllocator<BlockId>` from `Framework.Core` (ids piece). Port `CommandService.Execute`. Add tests: command inside command → inner `CauseId` = outer `Id`; throw → `Errored`; `OperationCanceledException` → `Cancelled`; async body that awaits a `Task` keeps the block open.
6. **Open kinds.** Replace the `BlockKind` enum with the records and `BlockKindRegistry.WithBaselines()`. Register it in `BlocksInstaller`.
7. **Source.** Add `ISourced`. `CommandService` builds `BlockHeader.Of(...)`.
8. **Rules.** Add BLK-01 (with the new `type` parameter on `il.callersOnly`) and BLK-03 (test-backed). BLK-04 and BLK-05 stay `proposed`.
9. **Docs.** Remove the `Block*` exception from the "Wire carries no domain types" invariant in `docs/architecture.md`. Add `Blocks/` to the codemap. Mark `wire-block-events.md` L5 superseded by this spike.

## 8. Interfaces with the other three pieces

### Needs from the ids piece

- `BlockId` is defined in `Framework.Blocks` (decision 1). The ids piece owns its shape; this piece needs `Value`, `None`, equality, and an unmanaged layout for AutoPacker.
- `IdAllocator<BlockId>` from `Framework.Core`: `Next()` returns a value > 0, monotonic, unique per match. Server-only registration.
- `EntityId` from `Framework.Core`, for `SourceId` and `ISourced`.
- `BlockId.None` (`0`) is never allocated. It means "no block" and "no cause".
- Reset per match (`wire-block-events.md` L3).
- One instance for all levels. This removes the "second emitter" collision risk by construction.

### Needs from the router piece

- Nothing. The router gives no `int` slot for each message type, so `BlockStartEvent` has no `Message` field (see Decisions).

### Gives to the pipeline piece

- `BlockService.Run` / `RunAsync` (`Task`-based) with a `BlockHeader`. The command and effect levels bracket through it. The intent and event levels open no block. No level constructs block events.
- `BlockService.Current`, to capture a cause when work is deferred.
- `BlockHeader.Of(kind, message, causeId)`, which reads `ISourced`.

### Needs from the pipeline piece

- **One flow at a time.** While a block is open across an `await`, the pump starts no other item that opens a block. Without this, the stream is not well nested and `Close` throws.
- Validation before `Run`.
- Capture of `Current` as `CauseId` when it schedules deferred work (After hooks, triggers).
- An `Answer` intent resumes the waiting `RunAsync` body. It must not open a block, so the resumed work stays inside the block that waits.
- Messages that have a source implement `ISourced`.

### Gives to client tools (presentation, phase 7)

- `IReadOnlyBlockStore` and the block events through `HandlerRegistry`, as listed in §5.

## 9. Open questions

1. **Shared id space.** Closed by decision 1: `BlockId` is its own type with its own `IdAllocator<BlockId>`, so it does not share a counter with request ids. This supersedes the shared counter in `wire-block-events.md` L3.
2. **Kind slot ranges.** Does the framework reserve a slot range (for example 0–63) for baseline kinds, or does the registry clash check suffice?
3. **`Trigger` fold.** Does presentation need a distinct trigger kind? Decide in the phase 4 reactions spike. The fold is reversible with one record.
4. **Concurrent flows.** If the pipeline must run two flows at once (for example, a timer while a request waits), the stream stops being well nested. Then the lexical parent needs its own wire field. Confirm with the pipeline piece.
5. **SBA and turn grouping.** Does the engine loop open an `Sba` or `Turn` block (`wire-network-protocol.md` "SBA wrapper block")? This piece supports it; the pipeline piece decides.
6. **Close check on the error path.** `Close` throws on an out-of-order close. On the error path, this new exception hides the original one. Keep the check, log and skip, or check only in tests?
7. **Hidden information.** Must `SourceId` be redacted for a hidden card (an opponent's face-down card)? This follows the hidden-information model, which is not designed yet.
8. **Message identity.** `Message` is dropped. If presentation needs "which command" without a source (for example a system command), who gives a stable slot: the router, or a new registry? A server-only debug log with the reflected payload is the other option for tools.
