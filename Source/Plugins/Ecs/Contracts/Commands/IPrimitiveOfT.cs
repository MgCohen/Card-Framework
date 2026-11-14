using VSA.Contracts.Commands;

namespace Ecs.Contracts.Commands;

public interface IPrimitive<TResult> : ICommand<TResult>, IPrimitive
{
}
