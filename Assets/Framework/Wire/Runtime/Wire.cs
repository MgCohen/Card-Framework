using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public static class Wire
    {
        public static WireConnection Connect(IPackingHandler handler = null)
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport, handler);
            var clientNetwork = new Network(clientTransport, handler);
            var serverStream  = new EventStream();
            var clientStream  = new EventStream();

            // Bridges subscribe in their ctor; no reference is kept (same as EngineLayer's DI wiring).
            _ = new WireForwarder(serverStream, serverNetwork);
            _ = new NetworkInboundBridge(clientNetwork, clientStream);

            return new WireConnection(serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
