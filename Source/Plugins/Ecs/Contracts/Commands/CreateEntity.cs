using Ecs.Domain.Shared;
using MemoryPack;

namespace Ecs.Contracts.Commands;

[MemoryPackable]
public sealed partial record CreateEntity(EntityId Entity) : IPrimitive<EntityId>;
