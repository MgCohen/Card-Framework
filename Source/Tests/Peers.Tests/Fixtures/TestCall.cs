using MemoryPack;
using Wire.Contracts.Commands;

namespace Peers.Tests.Fixtures;

public enum CallMode
{
    Echo,
    Refuse,
    Throw,
    Spawn,
}

// A client message for the tests. Its server handler selects the path from the mode: a reply only, an error, an
// exception, or primitives that change the World.
[MemoryPackable]
public sealed partial record TestCall(CallMode Mode, int Value) : IClientMessage<int>;
