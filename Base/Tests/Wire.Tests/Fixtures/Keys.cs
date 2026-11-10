using Ecs.Domain.Shared;

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
