using MemoryPack;
using VSA.Contracts;

namespace Wire.Contracts.Commands;

[MemoryPackable]
public sealed partial record WireHello(ulong Hash) : IMessage;
