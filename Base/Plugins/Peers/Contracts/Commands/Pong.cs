using MemoryPack;

namespace Peers.Contracts.Commands;

[MemoryPackable]
public sealed partial record Pong(int Sequence);
