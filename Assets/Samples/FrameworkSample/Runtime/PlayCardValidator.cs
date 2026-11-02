using System;
using Framework.Pipeline;

namespace Framework.Samples.FrameworkSample
{
    public sealed class PlayCardValidator : Validator<GameMessage>
    {
        readonly CardStore _cards;

        public PlayCardValidator(CardStore cards) => _cards = cards;

        public override Type RouteType => typeof(PlayCard);

        protected override bool Validate(GameMessage message, out string reason)
        {
            var play = (PlayCard)message;
            if (_cards.TryGetById(play.Card, out var card) && card.Zone is Zone.Hand)
            {
                reason = null;
                return true;
            }
            reason = $"Card {play.Card.Value} is not in Hand.";
            return false;
        }
    }
}
