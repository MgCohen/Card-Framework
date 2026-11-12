using MemoryPack;
using VSA.Contracts;

namespace Wire.Contracts.Commands;

// The result of one client message, sent to that client only. Value holds the MemoryPack bytes of the result.
[MemoryPackable]
public sealed partial record WireReply(string? ErrorCode, string? ErrorMessage, byte[] Value) : IMessage;
