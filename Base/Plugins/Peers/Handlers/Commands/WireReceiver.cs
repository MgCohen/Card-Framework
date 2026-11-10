using Network.Domain;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

public sealed class WireReceiver : IDisposable
{
    private readonly WireCatalog catalog;
    private readonly ICommandSender bus;
    private readonly WireLog log;
    private readonly ITransport server;

    public WireReceiver(WireCatalog catalog, ICommandSender bus, WireLog log, ITransport server)
    {
        this.catalog = catalog;
        this.bus = bus;
        this.log = log;
        this.server = server;
    }

    public Task Idle => throw new NotImplementedException((catalog, bus, log, server).ToString());

    public void Dispose()
    {
    }
}
