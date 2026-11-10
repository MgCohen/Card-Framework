using Ecs.Domain.Shared;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Cards.Slices.PlayCard;

public sealed record PlayCardCommand(EntityId Card, EntityId Player) : ICommand<Unit>;
