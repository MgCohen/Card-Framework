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
    }
}
