using Framework.Wire;

namespace Framework.States
{
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
