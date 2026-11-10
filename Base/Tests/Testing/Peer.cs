using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Network.Domain;
using Peers.Handlers.Commands;
using VSA.Composition;

namespace Testing;

// One peer: its own app host and its own Match. The role selects the services, as EngineInstaller(isServer) does in
// the Unity Framework: a server sends its log to each client transport, and a client applies the frames it receives.
public sealed class Peer : IDisposable
{
    private readonly IHost host;

    private Peer(IHost host, Match match)
    {
        this.host = host;
        Match = match;
    }

    public Match Match { get; }

    public static Peer Start(PeerRole role, IEnumerable<ITransport> transports, Action<IServiceCollection>? services = null)
    {
        IHost host = VsaBuilder.Create().UseGenericHost().Services(s =>
        {
            foreach (ITransport transport in transports)
            {
                s.AddSingleton(transport);
            }

            if (role == PeerRole.Server)
            {
                s.AddScoped<WireSender>();
            }
            else
            {
                s.AddScoped<WireReceiver>();
            }

            services?.Invoke(s);
        }).Build();

        Match match = new Match(host.Services);
        if (role == PeerRole.Server)
        {
            match.Services.GetRequiredService<WireSender>();
        }
        else
        {
            match.Services.GetRequiredService<WireReceiver>();
        }

        return new Peer(host, match);
    }

    public void Dispose()
    {
        Match.Dispose();
        host.Dispose();
    }
}
