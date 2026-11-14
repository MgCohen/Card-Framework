using Ecs.Domain.Shared;

namespace Ecs.Tests.Fixtures;

public struct Health : IValue<int>
{
    public int Value { get; init; }
}

public struct Position : IComponent
{
    public int X { get; init; }

    public int Y { get; init; }
}

public struct Owner : IComponent
{
    public EntityId Player { get; init; }
}

public struct CardTag : ITag
{
}
