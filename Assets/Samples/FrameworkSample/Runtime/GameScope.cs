using Framework.Blocks;
using Framework.Pipeline;
using Framework.Wire;

namespace Framework.Samples.FrameworkSample
{
    public sealed class GameScope : PipelineScope
    {
        readonly CardStore _cards;
        readonly IEventEmitter _events;
        readonly ZoneRegistry _zones;

        public GameScope(CardStore cards, GroupService groups, IEventEmitter events, ZoneRegistry zones)
        {
            _cards = cards;
            Groups = groups;
            _events = events;
            _zones = zones;
        }

        public GroupService Groups { get; }

        public void Move(CardId id, Zone to)
        {
            ThrowIfClosed();
            var card = _cards.ById(id);
            _cards.Put(card with { Zone = to });
            _events.Emit(new CardMoved { Card = id, From = _zones.SlotOf(card.Zone), To = _zones.SlotOf(to) });
        }
    }
}
