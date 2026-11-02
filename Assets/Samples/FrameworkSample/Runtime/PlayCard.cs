namespace Framework.Samples.FrameworkSample
{
    public sealed class PlayCard : GameMessage
    {
        public PlayCard(CardId card) => Card = card;

        public CardId Card { get; }
    }
}
