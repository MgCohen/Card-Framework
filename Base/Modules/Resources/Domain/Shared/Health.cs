using Ecs.Domain.Shared;

namespace Resources.Domain.Shared;

public struct Health : IValue<int>
{
    public int Value { get; init; }
}
