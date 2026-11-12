using MemoryPack;
using Wire.Contracts.Commands;

namespace Wire.Tests.Fixtures;

// A client message for the catalog tests. No handler: the catalog only needs its type.
[MemoryPackable]
public sealed partial record Shout(string Text) : IClientMessage<int>;
