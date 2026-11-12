using Ecs.Domain;
using Ecs.Domain.Shared;

namespace Ecs.Infrastructure;

internal sealed class WorldWriter : IWorldWriter
{
    private readonly World world;

    internal WorldWriter(World world)
    {
        this.world = world;
    }

    public int Count => world.Count;

    public Entity Create()
    {
        return world.Create();
    }

    public Entity Create(EntityId id)
    {
        return world.Create(id);
    }

    public EntityId NextId()
    {
        return world.NextId();
    }

    public bool Delete(Entity entity)
    {
        return world.Delete(entity);
    }

    public bool Set<T>(Entity entity, T value)
        where T : struct, IComponent
    {
        return world.Set(entity, value);
    }

    public bool Set<T>(Entity entity, int value)
        where T : struct, IValue<int>
    {
        return world.Set<T>(entity, value);
    }

    public bool Remove<T>(Entity entity)
        where T : struct, IComponent
    {
        return world.Remove<T>(entity);
    }

    public bool AddTag<T>(Entity entity)
        where T : struct, ITag
    {
        return world.AddTag<T>(entity);
    }

    public bool RemoveTag<T>(Entity entity)
        where T : struct, ITag
    {
        return world.RemoveTag<T>(entity);
    }

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
