# Entities

Entities give every game object (player, zone, card, modifier) an integer id, a type and an optional definition id, created on the server and mirrored to clients.

**Code:** `Assets/Entities/` · assembly `Framework.Entities` · depends on `Framework.Core`, `Framework.Wire`, `Framework.Tags` (referenced, not used by runtime code), `Scaffold.Autopacker`

## What it does
- Defines `Entity`: an immutable `Id`, `Type` and `DefinitionId`. Game data lives in tags, not on the entity.
- Defines entity kinds as `EntityType` records over `TypeKey`. Baselines: `Unknown` (0), `Player` (2), `Zone` (3), `Card` (4), `Modifier` (5). Slot 1 is left free.
- Allocates ids on the server in `EntityService`, starting at 1.
- Stores entities by id in `EntityStore`. Insert is idempotent, so a replayed event does not duplicate.
- Provides `Catalog<TDef>` to hold static definitions (`IEntityDefinition`) by non-zero id. Id 0 means "no definition".

## How it works
1. Server code calls `EntityService.Create(type, definitionId)`. The service takes the next id.
2. The service emits `EntityCreatedEvent { EntityId, TypeSlot, DefinitionId }`.
3. `HandlerRegistry` calls `EntityCreatedHandler`. It resolves the type with `EntityTypeRegistry.BySlot` and calls the internal `EntityStore.Insert`.
4. Dispatch is synchronous, so `Create` returns `_store.ById(id)`, the entity the handler just inserted.
5. `WireForwarder` sends the event. On the client the same handler inserts the entity into the client `EntityStore` with the server's id.
6. `EntityInstaller` registers the store, `EntityTypeRegistry.WithBaselines()` and the handler on both ends, and `EntityService` on the server only.

## Key types
| Type | Job |
|---|---|
| `Entity` | Id, type and definition id. Constructor is internal. |
| `EntityType` | Record key for an entity kind. Baselines in `EntityType.cs`. |
| `EntityTypeRegistry` | `SlotRegistry<EntityType>`. `WithBaselines()` registers the five baselines. |
| `EntityService` | Server-side author. Allocates ids and emits `EntityCreatedEvent`. |
| `EntityStore` | Entities by id. Internal `Insert`; public `ById`, `TryGetById`. |
| `IReadOnlyEntityStore` | Read view exposed on `GameEngine.EntityStore`. |
| `EntityCreatedEvent` | Wire event for a new entity. |
| `EntityCreatedHandler` | Apply handler. The only caller of `EntityStore.Insert`. |
| `Catalog<TDef>` / `IEntityDefinition` | Static definitions by id. |

## Example
From `Assets/Entities/Tests/MirrorIntegrationTests.cs`:
```csharp
var (server, client, _) = Build();   // two TestStacks joined by Wire.Connect()

var serverCard = server.Entities.Create(new Card(), definitionId: 101);

Assert.IsTrue(client.EntityStore.TryGetById(serverCard.Id, out var clientCard));
Assert.AreEqual(new Card(), clientCard.Type);
Assert.AreEqual(101, clientCard.DefinitionId);
```

## Gaps
- No destroy. There is no event, service method or store writer to remove an entity.
- No queries or indexes. The store answers only by id and cannot enumerate, so code cannot find all cards or all entities of a type.
- `Catalog<TDef>.Add` is internal, and no runtime code fills a catalog. Only tests call it.
- `Framework.Entities` references `Framework.Tags`, but no runtime file uses it.
