using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Ecs.Infrastructure;

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
