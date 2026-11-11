using System.Runtime.InteropServices;
using Ecs.Domain.Shared;
using Friflo.Engine.ECS;
using FrifloEntity = Friflo.Engine.ECS.Entity;
using IComponent = Ecs.Domain.Shared.IComponent;
using ITag = Ecs.Domain.Shared.ITag;

namespace Ecs.Domain;

public sealed class World
{
    private readonly EntityStore store;
    private int lastId;

    internal World(EntityStore store)
    {
        this.store = store;
    }

    internal int Count => store.Count;

    internal Entity Create()
    {
        return Create(NextId());
    }

    internal Entity Create(EntityId id)
    {
        if (id.Value <= 0 || TryById(id, out _))
        {
            throw new ArgumentException($"The id {id.Value} is 0, negative or live.", nameof(id));
        }

        lastId = Math.Max(lastId, id.Value);
        return new Entity(store.CreateEntity(id.Value));
    }

    internal EntityId NextId()
    {
        return new EntityId(lastId + 1);
    }

    internal bool Delete(Entity entity)
    {
        Live(entity).DeleteEntity();
        return true;
    }

    internal bool Set<T>(Entity entity, T value)
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

    internal bool Set<T>(Entity entity, int value)
        where T : unmanaged, IValue<int>
    {
        return Set(entity, new T { Value = value });
    }

    internal bool Remove<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        return Live(entity).RemoveComponent<T>();
    }

    internal bool AddTag<T>(Entity entity)
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

    internal bool RemoveTag<T>(Entity entity)
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

    internal Entity ById(EntityId id)
    {
        if (!TryById(id, out Entity entity))
        {
            throw new KeyNotFoundException($"No live entity has the id {id.Value}.");
        }

        return entity;
    }

    internal bool TryById(EntityId id, out Entity entity)
    {
        if (id.Value > 0 && store.TryGetEntityById(id.Value, out FrifloEntity found) && !found.IsNull)
        {
            entity = new Entity(found);
            return true;
        }

        entity = default;
        return false;
    }

    internal T Get<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        if (!TryGet(entity, out T value))
        {
            throw new KeyNotFoundException($"The entity {entity.Id.Value} has no {typeof(T).Name}.");
        }

        return value;
    }

    internal bool TryGet<T>(Entity entity, out T value)
        where T : unmanaged, IComponent
    {
        return Live(entity).TryGetComponent(out value);
    }

    internal bool Has<T>(Entity entity)
        where T : unmanaged, IComponent
    {
        return Live(entity).HasComponent<T>();
    }

    internal bool HasTag<T>(Entity entity)
        where T : struct, ITag
    {
        return Live(entity).Tags.Has<T>();
    }

    internal bool Is<T>(Entity entity, T value)
        where T : unmanaged, IComponent
    {
        return Live(entity).TryGetComponent(out T current) && SameBytes(ref current, ref value);
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
        if (entity.IsNull)
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
