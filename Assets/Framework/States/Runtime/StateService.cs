using Framework.Wire;

namespace Framework.States
{
    // Push surface over the FSM cursor. Server-only — clients read StateStore directly and
    // never author transitions. Push emits a slot-discriminated StatePushEvent; the store write
    // happens exclusively through StatePushHandler, the same path on server (this scope) and
    // client (via the wire). No double-write. State is zero-data, so Push takes no instance —
    // the type names the slot (new TState().Slot).
    public sealed class StateService
    {
        readonly IEventEmitter _events;

        public StateService(IEventEmitter events)
        {
            _events = events;
        }

        public void Push<TState>() where TState : GameState, new()
            => _events.Emit(new StatePushEvent { Slot = new TState().Slot });
    }
}
