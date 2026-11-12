namespace Ecs.Domain.Shared;

public interface IValue<T> : IComponent
{
    T Value { get; init; }
}
