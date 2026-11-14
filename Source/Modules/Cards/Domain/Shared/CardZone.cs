using Ecs.Domain.Shared;

namespace Cards.Domain.Shared;

public struct CardZone : IValue<Zone>
{
    public Zone Value { get; init; }
}
