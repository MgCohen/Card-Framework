using Ecs.Domain.Shared;
using MemoryPack;
using VSA.Contracts;

namespace Ecs.Contracts.Commands;

[MemoryPackable]
public sealed partial record RemoveTag<T>(EntityId Entity) : IPrimitive<Unit>
    where T : struct, ITag;
