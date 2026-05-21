using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerJoinedEvent : WireEvent<PeerJoinedEvent.Packed>
    {
        [Packed] public int Seat;
        [Packed] public ulong NetworkId;
    }
}
