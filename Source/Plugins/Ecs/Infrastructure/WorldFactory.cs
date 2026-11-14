using Ecs.Domain;
using Friflo.Engine.ECS;

namespace Ecs.Infrastructure;

public sealed class WorldFactory : IWorldFactory
{
    public World Create()
    {
        EntityStore store = new EntityStore(PidType.UsePidAsId)
        {
            RecycleIds = false,
        };
        return new World(store);
    }
}
