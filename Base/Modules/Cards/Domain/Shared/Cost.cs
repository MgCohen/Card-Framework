using Ecs.Domain.Shared;

namespace Cards.Domain.Shared;

public struct Cost : IValue<int>
{
    public int Value { get; init; }
}
