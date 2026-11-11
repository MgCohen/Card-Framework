using Cards.Domain.Shared;
using Ecs.Domain;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts;
using VSA.Handlers;

namespace Cards.Slices.MoveCard;

public sealed class MoveCardValidator : Validator<MoveCardCommand>
{
    public override Error? Validate(IEnvelope<MoveCardCommand> envelope)
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        MoveCardCommand move = envelope.Message;
        if (!world.TryById(move.Card, out Entity card) || !world.HasTag<Card>(card))
        {
            return CardErrors.NotACard;
        }

        return world.TryGet(card, out CardZone zone) && zone.Value == move.To ? CardErrors.AlreadyThere : null;
    }
}
