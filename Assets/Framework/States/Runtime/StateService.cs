using Framework.Wire;

namespace Framework.States
{
    // Push surface over the FSM cursor. Server-only — clients read StateStore directly and
    // never author transitions. Push emits a StatePushEvent<TState>; the store write happens
    // exclusively through StatePushHandler<TState>, the same path on server (this scope) and
    // client (via the wire). No double-write. State is zero-data, so Push takes no instance —
    // the type is the value.
    public sealed class StateService
    {
        readonly IEventEmitter _events;

        public StateService(IEventEmitter events)
        {
            _events = events;
        }

        public void Push<TState>() where TState : GameState, new()
            => _events.Emit(new StatePushEvent<TState>());
    }
}
