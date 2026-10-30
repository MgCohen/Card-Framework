namespace Framework.Wire
{
    internal sealed class NetworkInboundBridge
    {
        public NetworkInboundBridge(Network network, IEventEmitter emitter)
            => network.OnReceived += emitter.Emit;
    }
}
