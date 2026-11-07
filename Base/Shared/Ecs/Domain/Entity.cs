using Ecs.Domain.Shared;
using FrifloEntity = Friflo.Engine.ECS.Entity;

namespace Ecs.Domain;

public readonly struct Entity : IEquatable<Entity>
{
    internal readonly FrifloEntity Inner;

    internal Entity(FrifloEntity inner)
    {
        Inner = inner;
    }

    public EntityId Id => new EntityId(Inner.Id);

    public bool IsNull => Inner.IsNull;

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public bool Equals(Entity other)
    {
        return Inner.Id == other.Inner.Id && ReferenceEquals(Inner.Store, other.Inner.Store);
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Inner.Id;
    }
}
