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

    public EntityId Id => throw new NotImplementedException();

    public bool IsNull => throw new NotImplementedException();

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);

    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public bool Equals(Entity other)
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
