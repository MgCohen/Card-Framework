namespace Framework.Samples.FrameworkSample
{
    public sealed class GameView
    {
        readonly CardStore _cards;

        public GameView(CardStore cards) => _cards = cards;

        public bool TryGetZone(CardId id, out Zone zone)
        {
            zone = _cards.TryGetById(id, out var card) ? card.Zone : null;
            return zone != null;
        }
    }
}
