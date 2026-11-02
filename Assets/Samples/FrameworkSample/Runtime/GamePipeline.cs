using Framework.Blocks;
using Framework.Pipeline;
using Framework.Wire;

namespace Framework.Samples.FrameworkSample
{
    public sealed class GamePipeline : Pipeline<GameMessage, GameScope, GameView>
    {
        readonly CardStore _cards;
        readonly GroupService _groups;
        readonly IEventEmitter _events;
        readonly ZoneRegistry _zones;
        readonly GameView _view;

        public GamePipeline(PipelineServices<GameMessage, GameScope, GameView> services,
                            CardStore cards, GroupService groups, IEventEmitter events, ZoneRegistry zones)
            : base(services)
        {
            _cards = cards;
            _groups = groups;
            _events = events;
            _zones = zones;
            _view = new GameView(cards);
        }

        protected override GameScope CreateScope(Envelope<GameMessage> e) => new(_cards, _groups, _events, _zones);

        protected override GameView CreateView(Envelope<GameMessage> e) => _view;
    }
}
