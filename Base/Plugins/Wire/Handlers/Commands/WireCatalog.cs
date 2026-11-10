using System.Buffers;
using Ecs.Contracts.Commands;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Wire.Handlers.Commands;

public sealed class WireCatalog
{
    public WireCatalog(IEnumerable<WireTypes> types)
    {
    }

    public int Count => throw new NotImplementedException();

    public byte IdOf(Type type)
    {
        throw new NotImplementedException();
    }

    public void Write(IMessage entry, IBufferWriter<byte> output)
    {
        throw new NotImplementedException();
    }

    public IMessage Read(ReadOnlySpan<byte> frame)
    {
        throw new NotImplementedException();
    }

    public Task<Error?> SendAsync(IPrimitive primitive, ICommandSender bus, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
