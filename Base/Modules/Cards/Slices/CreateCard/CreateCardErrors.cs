using VSA.Contracts;

namespace Cards.Slices.CreateCard;

public static class CreateCardErrors
{
    public static readonly Error NegativeCost = new Error("Cards.NegativeCost", "The cost of a card is 0 or more.");
}
