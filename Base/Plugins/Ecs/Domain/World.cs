using System.Buffers;
using Ecs.Domain.Shared;
using Friflo.Engine.ECS;
using MemoryPack;
using FrifloEntity = Friflo.Engine.ECS.Entity;
using IComponent = Ecs.Domain.Shared.IComponent;
using ITag = Ecs.Domain.Shared.ITag;

namespace Ecs.Domain;

public sealed class World : IWorldWriter
{
    private readonly EntityStore store;
    private readonly ArrayBufferWriter<byte> leftBytes = new ArrayBufferWriter<byte>();
    private readonly ArrayBufferWriter<byte> rightBytes = new ArrayBufferWriter<byte>();
    private int lastId;

    internal World(EntityStore store)
    {
        this.store = store;
        Reader = new WorldReader(this);
    }

    public IWorldReader Reader { get; }

    public int Count => store.Count;

    public Entity Create()
    {
        return Create(NextId());
    }

    public Entity Create(EntityId id)
    {
        if (id.Value <= 0 || TryById(id, out _))
        {
            throw new ArgumentException($"The id {id.Value} is 0, negative or live.", nameof(id));
        }

        lastId = Math.Max(lastId, id.Value);
        return new Entity(store.CreateEntity(id.Value));
    }

    public EntityId NextId()
    {
        return new EntityId(lastId + 1);
    }

    public bool Delete(Entity entity)
    {
        Live(entity).DeleteEntity();
        return true;
    }

    public bool Set<T>(Entity entity, T value)
        where T : struct, IComponent
    {
        FrifloEntity live = Live(entity);
        if (live.TryGetComponent(out T current) && SameBytes(current, value))
        {
            return false;
        }

        live.AddComponent(value);
        return true;
    }

    public bool Set<T>(Entity entity, int value)
        where T : struct, IValue<int>
    {
        return Set(entity, new T { Value = value });
    }

    public bool Remove<T>(Entity entity)
        where T : struct, IComponent
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
        where T : struct, IComponent
    {
        if (!TryGet(entity, out T value))
        {
            throw new KeyNotFoundException($"The entity {entity.Id.Value} has no {typeof(T).Name}.");
        }

        return value;
    }

    public bool TryGet<T>(Entity entity, out T value)
        where T : struct, IComponent
    {
        return Live(entity).TryGetComponent(out value);
    }

    public bool Has<T>(Entity entity)
        where T : struct, IComponent
    {
        return Live(entity).HasComponent<T>();
    }

    public bool HasTag<T>(Entity entity)
        where T : struct, ITag
    {
        return Live(entity).Tags.Has<T>();
    }

    public bool Is<T>(Entity entity, T value)
        where T : struct, IComponent
    {
        return Live(entity).TryGetComponent(out T current) && SameBytes(current, value);
    }

    private bool SameBytes<T>(in T left, in T right)
        where T : struct
    {
        leftBytes.ResetWrittenCount();
        rightBytes.ResetWrittenCount();
        MemoryPackSerializer.Serialize(leftBytes, left);
        MemoryPackSerializer.Serialize(rightBytes, right);
        return leftBytes.WrittenSpan.SequenceEqual(rightBytes.WrittenSpan);
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
