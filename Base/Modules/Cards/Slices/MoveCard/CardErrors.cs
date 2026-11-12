using VSA.Contracts;

namespace Cards.Slices.MoveCard;

public static class CardErrors
{
    public static readonly Error NotACard = new Error(
        "Cards.NotACard", "No live entity with the Card tag has this id.");

    public static readonly Error AlreadyThere = new Error("Cards.AlreadyThere", "The card is already in this zone.");
}
