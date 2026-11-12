using Ecs.Domain.Shared;
using MemoryPack;

namespace Ecs.Contracts.Commands;

[MemoryPackable]
public sealed partial record ChangeComponent<T>(EntityId Entity, T Value) : IPrimitive<T>
    where T : struct, IComponent;
