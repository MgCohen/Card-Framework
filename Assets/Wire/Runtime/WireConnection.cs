using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public readonly struct WireConnection
    {
        public readonly EventStream ServerStream;
        public readonly EventStream ClientStream;
        public readonly Network ServerNetwork;
        public readonly Network ClientNetwork;

        public WireConnection(EventStream serverStream, EventStream clientStream,
                              Network serverNetwork, Network clientNetwork)
        {
            ServerStream  = serverStream;
            ClientStream  = clientStream;
            ServerNetwork = serverNetwork;
            ClientNetwork = clientNetwork;
        }

        // Builds a loopback server/client pair with both bridges.
        public static WireConnection Loopback(IPackingHandler handler = null)
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport, handler);
            var clientNetwork = new Network(clientTransport, handler);
            var serverStream  = new EventStream();
            var clientStream  = new EventStream();

            // Bridges subscribe in their ctor; no reference is kept (same as EngineInstaller's DI wiring).
            _ = new WireForwarder(serverStream, serverNetwork);
            _ = new NetworkInboundBridge(clientNetwork, clientStream);

            return new WireConnection(serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
