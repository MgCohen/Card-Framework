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

            serverStream.OnAppended  += serverNetwork.Send;
            clientNetwork.OnReceived += clientStream.Emit;

            return new WireConnection(serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
