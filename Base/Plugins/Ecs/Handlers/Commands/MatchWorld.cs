using Ecs.Domain;

namespace Ecs.Handlers.Commands;

public sealed class MatchWorld
{
    private World? bound;

    public IWorldReader Reader => Bound().Reader;

    internal IWorldWriter Writer => Bound();

    internal World World => Bound();

    internal void Bind(World world)
    {
        if (bound != null)
        {
            throw new InvalidOperationException("This scope already has the World of a match.");
        }

        bound = world;
    }

    private World Bound()
    {
        return bound ?? throw new InvalidOperationException(
            "No match owns this scope. Make a Match, then send and read through its scope.");
    }
}
