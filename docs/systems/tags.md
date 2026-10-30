# Tags

Tags store typed values on entities, keyed by entity id and tag slot, and keep server and client copies in sync through wire events.

**Code:** `Assets/Tags/` · assembly `Framework.Tags` · depends on `Framework.Core`, `Framework.Wire`, `Scaffold.Maps`, `Scaffold.Autopacker`

## What it does
- Declares tag keys as records: `Tag<T>(int Slot)` over `TypeKey`, where `T` is an unmanaged value type.
- Holds values in `TagStore`, one `Map<int, int, T>` per value type, keyed by entity id and slot.
- Lets server code author tags through `TagService.Set` and `TagService.Remove`.
- Emits `TagSetEvent<T>` for a new tag, `TagChangeEvent<T>` (with `Prev` and `Next`) for a changed one, and `TagRemovedEvent` for a removal.
- Skips the emit when `Set` writes the value already stored.
- Exposes reads through `IReadOnlyTagStore` (`Get<T>`, `Has`).

## How it works
1. Server code calls `TagService.Set(entityId, key, value)`. The service reads the store to pick the event: set, change, or nothing.
2. The service emits the event into the `EventStream` through `IEventEmitter`.
3. `HandlerRegistry` calls `TagSetHandler<T>`, `TagChangeHandler<T>` or `TagRemovedHandler`. The handler calls the internal `TagStore.Set` or `TagStore.Remove`.
4. `WireForwarder` sends the event to the client. The client stream runs the same handlers, which write the client `TagStore`.
5. `TagInstaller` registers `TagStore`, `TagRegistry` and the handlers on both ends. It registers `TagService` on the server only. Handlers exist for `int` and `bool`.

## Key types
| Type | Job |
|---|---|
| `Tag` / `Tag<T>` | Typed tag key. `Slot` identifies the tag; `ValueType` names `T`. |
| `TagService` | Server-side author. Reads the store, emits set, change or remove events. |
| `TagStore` | Per-value-type maps. Internal `Set` and `Remove`; public `Get` and `Has`. |
| `IReadOnlyTagStore` | Read view exposed on `GameEngine.TagStore`. |
| `TagSetEvent<T>` / `TagChangeEvent<T>` / `TagRemovedEvent` | Wire events for each kind of write. |
| `TagSetHandler<T>` / `TagChangeHandler<T>` / `TagRemovedHandler` | Apply handlers. The only store writers. |
| `TagRegistry` | `SlotRegistry<Tag>` for tag keys. |

## Example
From `Assets/Entities/Tests/MirrorIntegrationTests.cs`:
```csharp
public sealed record HealthTag() : Tag<int>(1001);   // SampleTagsContainer.cs

var serverCard = server.Entities.Create(new Card());
server.Tags.Set(serverCard.Id, SampleTags.Health, 30);
server.Tags.Set(serverCard.Id, SampleTags.Health, 25);  // emits TagChangeEvent<int>

Assert.AreEqual(25, client.TagStore.Get<int>(serverCard.Id, SampleTags.Health.Slot));
```

## Gaps
- `TagRemovedEvent` carries only `EntityId` and `Slot`. The removed value is lost, so the event cannot be reversed.
- `Framework.Tags.Tests` is an empty assembly. Tag behaviour is tested only inside `Framework.Entities.Tests` (ARCH-08 exception until test suite PR T-3).
- `TagStore.Has` and `TagStore.Remove` scan every value-type map, because the slot alone does not say which map holds it.
- Value types are fixed to `int` and `bool` in two places: `TagInstaller` and `WireRegistrar.ValueTypes`.
- `TagRegistry` is registered in DI, but no code adds tags to it or reads from it.
