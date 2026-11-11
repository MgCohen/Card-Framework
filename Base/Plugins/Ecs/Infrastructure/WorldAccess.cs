using Ecs.Domain;

namespace Ecs.Infrastructure;

internal static class WorldAccess
{
    internal static IWorldReader Reader(this World world)
    {
        return new WorldReader(world);
    }

    internal static IWorldWriter Writer(this World world)
    {
        return new WorldWriter(world);
    }

    internal static IWorldReader Reader(this MatchWorld match)
    {
        return match.World.Reader();
    }

    internal static IWorldWriter Writer(this MatchWorld match)
    {
        return match.World.Writer();
    }
}
