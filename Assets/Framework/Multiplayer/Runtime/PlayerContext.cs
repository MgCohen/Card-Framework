namespace Framework.Multiplayer
{
    public class PlayerContext
    {
        public ulong NetworkId { get; set; }
        public int Seat { get; set; }
        public ulong PlayerId { get; set; }
    }
}
