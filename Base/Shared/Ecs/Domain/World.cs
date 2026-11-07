using Ecs.Domain.Shared;

namespace Ecs.Domain;

public sealed class World : IWorldWriter
{
    public IWorldReader Reader => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public Entity Create()
    {
        throw new NotImplementedException();
    }

    public bool Delete(Entity entity)
    {
        throw new NotImplementedException();
    }

    public bool Set<T>(Entity entity, T value)
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public bool Set<T>(Entity entity, int value)
        where T : unmanaged, IValue<int>
    {
        throw new NotImplementedException();
    }

    public bool Remove<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public bool AddTag<T>(Entity entity)
        where T : struct, ITag
    {
        throw new NotImplementedException();
    }

    public bool RemoveTag<T>(Entity entity)
        where T : struct, ITag
    {
        throw new NotImplementedException();
    }

    public Entity ById(EntityId id)
    {
        throw new NotImplementedException();
    }

    public bool TryById(EntityId id, out Entity entity)
    {
        throw new NotImplementedException();
    }

    public T Get<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public bool TryGet<T>(Entity entity, out T value)
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public bool Has<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public bool HasTag<T>(Entity entity)
        where T : struct, ITag
    {
        throw new NotImplementedException();
    }
}
