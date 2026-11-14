namespace Ecs.Domain;

public sealed class MatchWorld : IDisposable
{
    private World? bound;

    internal World World => Bound();

    // The scope disposes this service when the match ends. Each reader and writer of the World then throws.
    public void Dispose()
    {
        bound?.End();
    }

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
            "No match owns this scope. Make a match with MatchFactory, then send and read through its scope.");
    }
}
