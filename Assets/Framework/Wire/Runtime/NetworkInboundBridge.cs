namespace Framework.Wire
{
    // Client-side bridge: every event delivered by Network.OnReceived is emitted into the
    // local EventStream, where Apply handlers dispatch by runtime type. One line, no
    // per-type switch — IEventEmitter.Emit(WireEvent) already routes by GetType().
    internal sealed class NetworkInboundBridge
    {
        public NetworkInboundBridge(Network network, IEventEmitter emitter)
            => network.OnReceived += emitter.Emit;
    }
}
