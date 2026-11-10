using VSA.Contracts;
using VSA.Handlers;

namespace Cards.Slices.MoveCard;

public sealed class MoveCardValidator : Validator<MoveCardCommand>
{
    public override Error? Validate(IEnvelope<MoveCardCommand> envelope)
    {
        return null;
    }
}
