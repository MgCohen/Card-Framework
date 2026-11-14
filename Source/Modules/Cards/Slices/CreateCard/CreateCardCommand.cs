using Cards.Domain.Shared;
using Ecs.Domain.Shared;
using VSA.Contracts.Commands;

namespace Cards.Slices.CreateCard;

public sealed record CreateCardCommand(Zone Zone) : ICommand<EntityId>;
