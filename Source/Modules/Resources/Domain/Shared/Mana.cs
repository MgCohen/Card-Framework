using Ecs.Domain.Shared;

namespace Resources.Domain.Shared;

public struct Mana : IValue<int>
{
    public int Value { get; init; }
}
