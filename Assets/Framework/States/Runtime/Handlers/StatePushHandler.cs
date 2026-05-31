using Framework.Wire;

namespace Framework.States
{
    // Sole writer for StatePushEvent<TState> on both sides. Server: StateService.Push emits —
    // this handler runs synchronously and writes the store. Client: the wire delivers the
    // event — same handler, same write. State is zero-data, so Apply reconstructs the cursor
    // as `new TState()` (identity is the type). Registered per state via RegisterStateType<TState>.
    internal sealed class StatePushHandler<TState> : WireEventHandler<StatePushEvent<TState>>
        where TState : GameState, new()
    {
        readonly StateStore _store;
        public StatePushHandler(StateStore store) { _store = store; }
        public override void Apply(StatePushEvent<TState> ev) => _store.Set(new TState());
    }
}
