using Network.Domain;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

public sealed class WireSender : IDisposable
{
    private readonly WireLog log;
    private readonly WireCatalog catalog;
    private readonly ITransport[] clients;

    public WireSender(WireLog log, WireCatalog catalog, IEnumerable<ITransport> clients)
    {
        this.log = log;
        this.catalog = catalog;
        this.clients = clients.ToArray();
    }

    public void Dispose()
    {
        _ = (log, catalog, clients);
    }
}
