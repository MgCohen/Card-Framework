using Scaffold.AutoPacker;

namespace Framework.Wire
{
    /// <summary>
    /// EventStream ↔ Network bridge. Constructor splits per direction so the wire
    /// shape matches per-end Networks: outbound on the server side, inbound on the
    /// client side. <see cref="Connect"/> builds the loopback-paired pair for tests.
    /// </summary>
    public sealed class Wire
    {
        /// <summary>Outbound bridge — appended events flow into the network.</summary>
        public Wire(EventStream source, Network network)
            => source.OnAppended += network.Send;

        /// <summary>Inbound bridge — received events flow into the sink stream.</summary>
        public Wire(Network network, EventStream sink)
            => network.OnReceived += sink.Add;

        /// <summary>
        /// Build a loopback-paired two-end setup for in-process tests. The caller
        /// still registers event types on each network (same order on both ends).
        /// </summary>
        public static (EventStream serverStream, EventStream clientStream,
                       Network serverNetwork, Network clientNetwork)
            Connect(IPackingHandler handler = null)
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport, handler);
            var clientNetwork = new Network(clientTransport, handler);

            var serverStream = new EventStream();
            var clientStream = new EventStream();

            _ = new Wire(serverStream, serverNetwork);   // outbound (server side)
            _ = new Wire(clientNetwork, clientStream);   // inbound (client side)

            return (serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
