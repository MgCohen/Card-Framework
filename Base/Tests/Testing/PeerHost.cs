using Microsoft.Extensions.DependencyInjection;
using Network.Domain;
using Peers.Handlers.Commands;

namespace Testing;

// One server and N clients in one process, each with its own app host and World. Each client has its own transport
// pair to the server. The clients start first, so each one receives the hello of the server.
public sealed class PeerHost : IDisposable
{
    private readonly List<Peer> clients;
    private readonly List<ITransport> serverSides;

    private PeerHost(Peer server, List<Peer> clients, List<ITransport> serverSides)
    {
        Server = server;
        this.clients = clients;
        this.serverSides = serverSides;
    }

    public Peer Server { get; }

    public IReadOnlyList<Peer> Clients => clients;

    public static PeerHost Start(int clients, Action<IServiceCollection>? services = null)
    {
        List<ITransport> serverSides = new List<ITransport>();
        List<Peer> started = new List<Peer>();
        for (int i = 0; i < clients; i++)
        {
            InProcessTransport.Pair(out InProcessTransport serverSide, out InProcessTransport clientSide);
            serverSides.Add(serverSide);
            started.Add(Peer.Start(PeerRole.Client, new ITransport[] { clientSide }, services));
        }

        return new PeerHost(Peer.Start(PeerRole.Server, serverSides, services), started, serverSides);
    }

    // The server stops its sends to this client and stops reading its frames.
    public void Disconnect(Peer client)
    {
        Server.WireServer.Disconnect(serverSides[clients.IndexOf(client)]);
    }

    public Task IdleAsync()
    {
        return Task.WhenAll(clients.Select(c => c.WireClient.Idle));
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
