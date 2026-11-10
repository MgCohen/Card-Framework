using VSA.Contracts;
using VSA.Handlers;

namespace Cards.Slices.CreateCard;

public sealed class CreateCardValidator : Validator<CreateCardCommand>
{
    public override Error? Validate(IEnvelope<CreateCardCommand> envelope)
    {
        return envelope.Message.Cost < 0 ? CreateCardErrors.NegativeCost : null;
    }
}
