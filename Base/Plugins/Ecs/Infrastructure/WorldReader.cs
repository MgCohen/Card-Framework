using Ecs.Domain;
using Ecs.Domain.Shared;

namespace Ecs.Infrastructure;

internal sealed class WorldReader : IWorldReader
{
    private readonly World world;

    internal WorldReader(World world)
    {
        this.world = world;
    }

    public int Count => world.Count;

    public Entity ById(EntityId id)
    {
        return world.ById(id);
    }

    public bool TryById(EntityId id, out Entity entity)
    {
        return world.TryById(id, out entity);
    }

    public T Get<T>(Entity entity)
        where T : struct, IComponent
    {
        return world.Get<T>(entity);
    }

    public bool TryGet<T>(Entity entity, out T value)
        where T : struct, IComponent
    {
        return world.TryGet(entity, out value);
    }

    public bool Has<T>(Entity entity)
        where T : struct, IComponent
    {
        return world.Has<T>(entity);
    }

    public bool HasTag<T>(Entity entity)
        where T : struct, ITag
    {
        return world.HasTag<T>(entity);
    }

    public bool Is<T>(Entity entity, T value)
        where T : struct, IComponent
    {
        return world.Is(entity, value);
    }
}
