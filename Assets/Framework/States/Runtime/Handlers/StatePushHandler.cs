using Framework.Wire;

namespace Framework.States
{
    internal sealed class StatePushHandler : WireEventHandler<StatePushEvent>
    {
        readonly StateStore _store;
        readonly GameStateRegistry _registry;

        public StatePushHandler(StateStore store, GameStateRegistry registry)
        {
            _store = store;
            _registry = registry;
        }

        public override void Apply(StatePushEvent ev) => _store.Set(_registry.BySlot(ev.Slot));
    }
}
