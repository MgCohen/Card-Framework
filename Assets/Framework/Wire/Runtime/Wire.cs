using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Wire
    {
        public Wire(EventStream source, Network network)
            => source.OnAppended += network.Send;

        public Wire(Network network, EventStream sink)
            => network.OnReceived += sink.Emit;

        public static (EventStream serverStream, EventStream clientStream,
                       Network serverNetwork, Network clientNetwork)
            Connect(IPackingHandler handler = null)
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport, handler);
            var clientNetwork = new Network(clientTransport, handler);

            var serverStream = new EventStream();
            var clientStream = new EventStream();

            _ = new Wire(serverStream, serverNetwork);
            _ = new Wire(clientNetwork, clientStream);

            return (serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
