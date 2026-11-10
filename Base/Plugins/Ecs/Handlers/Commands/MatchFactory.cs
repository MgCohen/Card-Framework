using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Ecs.Handlers.Commands;

// Makes one match: a new service scope with a new World bound to it. The caller owns the scope and disposes it.
public sealed class MatchFactory
{
    private readonly IServiceScopeFactory scopes;
    private readonly IWorldFactory worlds;

    public MatchFactory(IServiceScopeFactory scopes, IWorldFactory worlds)
    {
        this.scopes = scopes;
        this.worlds = worlds;
    }

    public IServiceScope Create()
    {
        IServiceScope scope = scopes.CreateScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<MatchWorld>().Bind(worlds.Create());
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        return scope;
    }
}
