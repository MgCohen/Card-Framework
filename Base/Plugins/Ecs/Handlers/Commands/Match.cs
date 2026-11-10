using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts.Commands;

namespace Ecs.Handlers.Commands;

public sealed class Match : IDisposable
{
    private readonly IServiceScope scope;
    private readonly MatchWorld world;
    private bool disposed;

    public Match(IServiceProvider app)
    {
        scope = app.CreateScope();
        try
        {
            world = scope.ServiceProvider.GetRequiredService<MatchWorld>();
            world.Bind(scope.ServiceProvider.GetRequiredService<IWorldFactory>().Create());
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public IServiceProvider Services => Live().ServiceProvider;

    public ICommandSender Commands => Services.GetRequiredService<ICommandSender>();

    public IWorldReader Reader
    {
        get
        {
            Live();
            return world.Reader;
        }
    }

    public void Dispose()
    {
        disposed = true;
        scope.Dispose();
    }

    private IServiceScope Live()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return scope;
    }
}
