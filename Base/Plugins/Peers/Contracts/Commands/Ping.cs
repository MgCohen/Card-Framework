using MemoryPack;
using Wire.Contracts.Commands;

namespace Peers.Contracts.Commands;

// A connection check: the server replies with a Pong that has the same sequence.
[MemoryPackable]
public sealed partial record Ping(int Sequence) : IClientMessage<Pong>;
