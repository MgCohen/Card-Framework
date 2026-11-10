using Ecs.Domain;

namespace Ecs.Handlers.Commands;

public sealed class MatchWorld
{
    public IWorldReader Reader => throw new NotImplementedException();

    internal IWorldWriter Writer => throw new NotImplementedException();

    internal void Bind(World world)
    {
        throw new NotImplementedException();
    }
}
