using Ecs.Domain.Shared;
using Resources.Domain.Shared;
using VSA.Contracts.Commands;

namespace Resources.Slices.ChangeResource;

public sealed record ChangeResourceCommand(EntityId Target, int Delta, Resource Resource = Resource.Health)
    : ICommand<int>;
