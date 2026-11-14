using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Network.Domain;
using Peers.Handlers.Commands;
using VSA.Composition;
using VSA.Contracts.Commands;

namespace Testing;

// One peer: its own app host and its own match scope. The role selects the services, as EngineInstaller(isServer) does
// in the Unity Framework: a server sends its log to each client transport and runs the messages of its clients, and a
// client applies the frames it receives and sends messages to the server.
public sealed class Peer : IDisposable
{
    private readonly IHost host;

    private Peer(IHost host, IServiceScope match)
    {
        this.host = host;
        Match = match;
    }

    public IServiceScope Match { get; }

    public IServiceProvider Services => Match.ServiceProvider;

    public ICommandSender Commands => Services.GetRequiredService<ICommandSender>();

    public IWorldReader Reader => Services.GetRequiredService<IWorldReader>();

    public WireServer WireServer => Services.GetRequiredService<WireServer>();

    public WireClient WireClient => Services.GetRequiredService<WireClient>();

    public static Peer Start(
        PeerRole role,
        IEnumerable<ITransport> transports,
        Action<IServiceCollection>? services = null)
    {
        IHost host = VsaBuilder.Create().UseGenericHost().Services(s =>
        {
            foreach (ITransport transport in transports)
            {
                s.AddSingleton(transport);
            }

            if (role == PeerRole.Server)
            {
                s.AddScoped<WireServer>();
            }
            else
            {
                s.AddScoped<WireClient>();
            }

            services?.Invoke(s);
        }).Build();

        IServiceScope? match = null;
        try
        {
            match = host.Services.GetRequiredService<MatchFactory>().Create();
            if (role == PeerRole.Server)
            {
                match.ServiceProvider.GetRequiredService<WireServer>();
            }
            else
            {
                match.ServiceProvider.GetRequiredService<WireClient>();
            }

            return new Peer(host, match);
        }
        catch
        {
            match?.Dispose();
            host.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Match.Dispose();
        host.Dispose();
    }
}
