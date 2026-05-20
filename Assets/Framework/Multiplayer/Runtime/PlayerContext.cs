namespace Framework.Multiplayer
{
    // Host-resolved shape. Built from PlayerData by the host-side ContextResolver,
    // broadcast to all peers via PlayerContextEvent. Same polymorphism caveat as
    // PlayerData — subclass fields don't yet survive the wire.
    public class PlayerContext
    {
        public ulong NetworkId;
        public int Seat;
        public ulong PlayerId;
    }
}
