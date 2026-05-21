using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public static class Wire
    {
        public static WireConnection Connect(IPackingHandler handler = null)
        {
            var transports = LoopbackTransport.Pair();
            var serverNetwork = new Network(transports.ServerSide, handler);
            var clientNetwork = new Network(transports.ClientSide, handler);
            var serverStream  = new EventStream();
            var clientStream  = new EventStream();

            serverStream.OnAppended  += serverNetwork.Send;
            clientNetwork.OnReceived += clientStream.Emit;

            return new WireConnection(serverStream, clientStream, serverNetwork, clientNetwork);
        }
    }
}
