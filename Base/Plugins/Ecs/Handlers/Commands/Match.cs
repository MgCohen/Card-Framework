using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts.Commands;

namespace Ecs.Handlers.Commands;

public sealed class Match : IDisposable
{
    private readonly IServiceScope scope;
    private readonly MatchWorld world;

    public Match(IServiceProvider app)
    {
        scope = app.CreateScope();
        world = scope.ServiceProvider.GetRequiredService<MatchWorld>();
        world.Bind(scope.ServiceProvider.GetRequiredService<IWorldFactory>().Create());
    }

    public IServiceProvider Services => scope.ServiceProvider;

    public ICommandSender Commands => Services.GetRequiredService<ICommandSender>();

    public IWorldReader Reader => world.Reader;

    internal World World => world.World;

    public void Dispose()
    {
        scope.Dispose();
    }
}
