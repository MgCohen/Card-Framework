using Ecs.Domain;

namespace Ecs.Handlers.Commands;

public sealed class MatchWorld
{
    private World? world;

    public IWorldReader Reader => Bound().Reader;

    internal IWorldWriter Writer => Bound();

    internal World World => Bound();

    internal void Bind(World match)
    {
        if (world != null)
        {
            throw new InvalidOperationException("This scope already has the World of a match.");
        }

        world = match;
    }

    private World Bound()
    {
        return world ?? throw new InvalidOperationException(
            "No match owns this scope. Make a Match, then send and read through its scope.");
    }
}
