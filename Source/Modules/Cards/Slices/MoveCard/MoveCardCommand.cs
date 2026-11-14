using Cards.Domain.Shared;
using Ecs.Domain.Shared;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Cards.Slices.MoveCard;

public sealed record MoveCardCommand(EntityId Card, Zone To) : ICommand<Unit>;
