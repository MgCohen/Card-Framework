using VSA.Contracts;

namespace Cards.Slices.PlayCard;

public static class PlayCardErrors
{
    public static readonly Error NotInHand = new Error("Cards.NotInHand", "The card is not in the hand.");

    public static readonly Error NoPlayer = new Error("Cards.NoPlayer", "No live entity has the player id.");

    public static readonly Error NotEnoughMana = new Error(
        "Cards.NotEnoughMana", "The player has less mana than the cost of the card.");
}
