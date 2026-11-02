using Framework.Blocks;
using Framework.Pipeline;
using Framework.RecordIds;
using Framework.TypeRouting;
using Framework.Wire;

namespace Framework.Samples.FrameworkSample
{
    public sealed class GamePipeline : Pipeline<GameMessage, GameScope>
    {
        readonly CardStore _cards;
        readonly GroupService _groups;
        readonly IEventEmitter _events;
        readonly ZoneRegistry _zones;

        public GamePipeline(TypeRouter<Handler<GameMessage, GameScope>> handlers,
                            TypeRouter<Validator<GameMessage>> validators,
                            IdAllocator<MessageId> ids,
                            CardStore cards, GroupService groups, IEventEmitter events, ZoneRegistry zones)
            : base(handlers, validators, ids)
        {
            _cards = cards;
            _groups = groups;
            _events = events;
            _zones = zones;
        }

        protected override GameScope CreateScope(Envelope<GameMessage> e) => new(_cards, _groups, _events, _zones);
    }
}
