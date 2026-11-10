using Microsoft.Extensions.DependencyInjection;
using Network.Domain;
using Peers.Handlers.Commands;

namespace Testing;

// One server and N clients in one process, each with its own app host and World. Each client has its own transport
// pair to the server. The clients start first, so each one receives the hello of the server.
public sealed class PeerHost : IDisposable
{
    private readonly List<Peer> clients;

    private PeerHost(Peer server, List<Peer> clients)
    {
        Server = server;
        this.clients = clients;
    }

    public Peer Server { get; }

    public IReadOnlyList<Peer> Clients => clients;

    public static PeerHost Start(int clients)
    {
        List<ITransport> serverSides = new List<ITransport>();
        List<Peer> started = new List<Peer>();
        for (int i = 0; i < clients; i++)
        {
            InProcessTransport.Pair(out InProcessTransport serverSide, out InProcessTransport clientSide);
            serverSides.Add(serverSide);
            started.Add(Peer.Start(PeerRole.Client, new ITransport[] { clientSide }));
        }

        return new PeerHost(Peer.Start(PeerRole.Server, serverSides), started);
    }

    public Task IdleAsync()
    {
        return Task.WhenAll(clients.Select(c => c.Match.Services.GetRequiredService<WireReceiver>().Idle));
    }

    public void Dispose()
    {
        Server.Dispose();
        foreach (Peer client in clients)
        {
            client.Dispose();
        }
    }
}
