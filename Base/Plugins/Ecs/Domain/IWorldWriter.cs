using Ecs.Domain.Shared;

namespace Ecs.Domain;

public interface IWorldWriter : IWorldReader
{
    Entity Create();

    bool Delete(Entity entity);

    bool Set<T>(Entity entity, T value)
        where T : unmanaged, IComponent;

    bool Set<T>(Entity entity, int value)
        where T : unmanaged, IValue<int>;

    bool Remove<T>(Entity entity)
        where T : unmanaged, IComponent;

    bool AddTag<T>(Entity entity)
        where T : struct, ITag;

    bool RemoveTag<T>(Entity entity)
        where T : struct, ITag;
}
