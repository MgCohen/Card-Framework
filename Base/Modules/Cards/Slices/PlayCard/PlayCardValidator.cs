using VSA.Contracts;
using VSA.Handlers;

namespace Cards.Slices.PlayCard;

public sealed class PlayCardValidator : Validator<PlayCardCommand>
{
    public override Error? Validate(IEnvelope<PlayCardCommand> envelope)
    {
        return null;
    }
}
