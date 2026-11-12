using MemoryPack;
using VSA.Contracts;

namespace Wire.Contracts.Commands;

[MemoryPackable]
public sealed partial record GroupEnd(BlockId Start, GroupOutcome Outcome) : IMessage;
