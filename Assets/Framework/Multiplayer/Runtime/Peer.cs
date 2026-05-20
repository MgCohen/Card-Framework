namespace Framework.Multiplayer
{
    public readonly struct Peer
    {
        public int Seat { get; }
        public ulong NetworkId { get; }
        public bool IsLocal { get; }
        public bool IsHost => Seat == 0;

        public Peer(int seat, ulong networkId, bool isLocal)
        {
            Seat = seat;
            NetworkId = networkId;
            IsLocal = isLocal;
        }
    }
}
