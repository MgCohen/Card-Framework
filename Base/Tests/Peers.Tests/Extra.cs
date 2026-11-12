using Ecs.Domain.Shared;

namespace Peers.Tests;

// A key that only one peer registers, so its catalog hash differs.
public struct Extra : IComponent
{
    public int Value { get; init; }
}
