using System.Runtime.InteropServices;
using Ecs.Domain.Shared;
using Friflo.Engine.ECS;
using FrifloEntity = Friflo.Engine.ECS.Entity;
using IComponent = Ecs.Domain.Shared.IComponent;
using ITag = Ecs.Domain.Shared.ITag;

namespace Ecs.Domain;

public sealed class World : IWorldWriter
{
    private readonly EntityStore store;

    public World()
    {
        store = new EntityStore { RecycleIds = false };
        Reader = new WorldReader(this);
    }

    public IWorldReader Reader { get; }

    public int Count => store.Count;

    public Entity Create()
    {
        return new Entity(store.CreateEntity());
    }

    public bool Delete(Entity entity)
    {
        Live(entity).DeleteEntity();
        return true;
    }

    public bool Set<T>(Entity entity, T value)
        where T : unmanaged, IComponent
    {
        FrifloEntity live = Live(entity);
        if (live.TryGetComponent(out T current) && SameBytes(ref current, ref value))
        {
            return false;
        }

        live.AddComponent(value);
        return true;
    }

    public bool Set<T>(Entity entity, int value)
        where T : unmanaged, IValue<int>
    {
        return Set(entity, new T { Value = value });
    }

    public bool Remove<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        return Live(entity).RemoveComponent<T>();
    }

    public bool AddTag<T>(Entity entity)
        where T : struct, ITag
    {
        FrifloEntity live = Live(entity);
        if (live.Tags.Has<T>())
        {
            return false;
        }

        live.AddTag<T>();
        return true;
    }

    public bool RemoveTag<T>(Entity entity)
        where T : struct, ITag
    {
        FrifloEntity live = Live(entity);
        if (!live.Tags.Has<T>())
        {
            return false;
        }

        live.RemoveTag<T>();
        return true;
    }

    public Entity ById(EntityId id)
    {
        if (!TryById(id, out Entity entity))
        {
            throw new KeyNotFoundException($"No live entity has the id {id.Value}.");
        }

        return entity;
    }

    public bool TryById(EntityId id, out Entity entity)
    {
        if (id.Value > 0 && store.TryGetEntityById(id.Value, out FrifloEntity found) && !found.IsNull)
        {
            entity = new Entity(found);
            return true;
        }

        entity = default;
        return false;
    }

    public T Get<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        if (!TryGet(entity, out T value))
        {
            throw new KeyNotFoundException($"The entity {entity.Id.Value} has no {typeof(T).Name}.");
        }

        return value;
    }

    public bool TryGet<T>(Entity entity, out T value)
        where T : unmanaged, IComponent
    {
        return Live(entity).TryGetComponent(out value);
    }

    public bool Has<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        return Live(entity).HasComponent<T>();
    }

    public bool HasTag<T>(Entity entity)
        where T : struct, ITag
    {
        return Live(entity).Tags.Has<T>();
    }

    private static bool SameBytes<T>(ref T left, ref T right)
        where T : unmanaged
    {
        ReadOnlySpan<byte> leftBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref left, 1));
        ReadOnlySpan<byte> rightBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref right, 1));
        return leftBytes.SequenceEqual(rightBytes);
    }

    private FrifloEntity Live(Entity entity)
    {
        if (entity.Inner.Store == null || entity.IsNull)
        {
            throw new InvalidOperationException($"The entity {entity.Id.Value} is null or deleted.");
        }

        if (!ReferenceEquals(entity.Inner.Store, store))
        {
            throw new InvalidOperationException($"The entity {entity.Id.Value} is in another world.");
        }

        return entity.Inner;
    }
}
