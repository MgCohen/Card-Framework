using Cards.Domain.Shared;
using Cards.Slices.MoveCard;
using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using VSA.Contracts;
using VSA.Handlers;

namespace Cards.Slices.PlayCard;

public sealed class PlayCardValidator : Validator<PlayCardCommand>
{
    public override Error? Validate(IEnvelope<PlayCardCommand> envelope)
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        PlayCardCommand play = envelope.Message;
        if (!world.TryById(play.Card, out Entity card) || !world.HasTag<Card>(card))
        {
            return CardErrors.NotACard;
        }

        if (!world.TryGet(card, out CardZone zone) || zone.Value != Zone.Hand)
        {
            return PlayCardErrors.NotInHand;
        }

        int cost = world.TryGet(card, out Cost value) ? value.Value : 0;
        bool hasMana = world.TryById(play.Player, out Entity player) && world.TryGet(player, out Mana pool);
        int mana = hasMana ? pool.Value : 0;
        return mana < cost ? PlayCardErrors.NotEnoughMana : null;
    }
}
