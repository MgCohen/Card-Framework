using System.Buffers;
using Network.Domain;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

public sealed class WireSender : IDisposable
{
    private readonly WireLog log;
    private readonly WireCatalog catalog;
    private readonly ITransport[] clients;
    private readonly ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

    public WireSender(WireLog log, WireCatalog catalog, IEnumerable<ITransport> clients)
    {
        this.log = log;
        this.catalog = catalog;
        this.clients = clients.ToArray();
        Send(new WireHello(catalog.Hash));
        log.Appended += Send;
    }

    public void Dispose()
    {
        log.Appended -= Send;
    }

    private void Send(IMessage entry)
    {
        frame.ResetWrittenCount();
        catalog.Write(entry, frame);
        foreach (ITransport client in clients)
        {
            client.Send(frame.WrittenSpan);
        }
    }
}
