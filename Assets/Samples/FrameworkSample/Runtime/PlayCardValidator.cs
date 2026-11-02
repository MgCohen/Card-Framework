using Framework.Pipeline;

namespace Framework.Samples.FrameworkSample
{
    public sealed class PlayCardValidator : Validator<GameMessage, PlayCard, GameView>
    {
        protected override bool Validate(PlayCard message, GameView view, out string reason)
        {
            reason = view.TryGetZone(message.Card, out var zone) && zone is Zone.Hand
                ? null
                : $"Card {message.Card.Value} is not in Hand.";
            return reason == null;
        }
    }
}
