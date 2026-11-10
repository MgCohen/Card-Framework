using Ecs.Domain.Shared;
using MemoryPack;
using VSA.Contracts;

namespace Ecs.Contracts.Commands;

[MemoryPackable]
public sealed partial record DeleteEntity(EntityId Entity) : IPrimitive<Unit>;
