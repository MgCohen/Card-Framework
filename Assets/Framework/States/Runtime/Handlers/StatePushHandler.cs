using Framework.Wire;

namespace Framework.States
{
    // Sole writer for StatePushEvent on both sides. Server: StateService.Push emits — this
    // handler runs synchronously and writes the store. Client: the wire delivers the event —
    // same handler, same write. One handler for all states: it resolves the cursor from the
    // event's int slot via the injected registry.
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
