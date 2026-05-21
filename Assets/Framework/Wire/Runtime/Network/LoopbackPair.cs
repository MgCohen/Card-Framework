namespace Framework.Wire
{
    public readonly struct LoopbackPair
    {
        public readonly LoopbackTransport ServerSide;
        public readonly LoopbackTransport ClientSide;

        public LoopbackPair(LoopbackTransport serverSide, LoopbackTransport clientSide)
        {
            ServerSide = serverSide;
            ClientSide = clientSide;
        }
    }
}
