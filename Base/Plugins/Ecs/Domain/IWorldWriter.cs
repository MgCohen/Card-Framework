using Ecs.Domain.Shared;

namespace Ecs.Domain;

public interface IWorldWriter : IWorldReader
{
    Entity Create();

    Entity Create(EntityId id);

    EntityId NextId();

    bool Delete(Entity entity);

    bool Set<T>(Entity entity, T value)
        where T : struct, IComponent;

    bool Set<T>(Entity entity, int value)
        where T : struct, IValue<int>;

    bool Remove<T>(Entity entity)
        where T : struct, IComponent;

    bool AddTag<T>(Entity entity)
        where T : struct, ITag;

    bool RemoveTag<T>(Entity entity)
        where T : struct, ITag;
}
