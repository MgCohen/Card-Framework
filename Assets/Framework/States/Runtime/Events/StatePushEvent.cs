using Framework.Wire;

namespace Framework.States
{
    // Transition event — one network channel per closed state type (mirrors TagSetEvent<T>).
    // Zero [Packed] fields: the type parameter IS the payload, so AutoPacker emits an empty
    // Packed struct for each concrete closed leaf (StatePushEvent<PlayingState>, ...). Inherits
    // WireEvent directly (post-flatten convention). Register one channel per state, both sides:
    // Network.Register<StatePushEvent<PlayingState>, StatePushEvent<PlayingState>.Packed>().
    public partial class StatePushEvent<TState> : WireEvent where TState : GameState, new() { }
}
