using Ecs.Domain.Shared;
using MemoryPack;

namespace Wire.Tests.Fixtures;

public struct Health : IComponent
{
    public int Value { get; init; }
}

public struct Armor : IComponent
{
    public int Value { get; init; }
}

public struct Marked : ITag
{
}

[MemoryPackable]
public partial struct CardName : IComponent
{
    public string Value { get; init; }
}
