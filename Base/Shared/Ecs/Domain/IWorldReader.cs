using Ecs.Domain.Shared;

namespace Ecs.Domain;

public interface IWorldReader
{
    int Count { get; }

    Entity ById(EntityId id);

    bool TryById(EntityId id, out Entity entity);

    T Get<T>(Entity entity)
        where T : unmanaged, IComponent;

    bool TryGet<T>(Entity entity, out T value)
        where T : unmanaged, IComponent;

    bool Has<T>(Entity entity)
        where T : unmanaged, IComponent;

    bool HasTag<T>(Entity entity)
        where T : struct, ITag;
}
