using Ecs.Domain.Shared;
using VSA.Contracts.Commands;

namespace Resources.Slices.ChangeResource;

public sealed record ChangeResourceCommand<T>(EntityId Target, int Delta) : ICommand<int>
    where T : struct, IValue<int>;
