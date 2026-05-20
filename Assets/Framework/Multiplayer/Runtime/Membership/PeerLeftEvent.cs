using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerLeftEvent : WireEvent<PeerLeftEvent.Packed>
    {
        [Packed] public int Seat;
    }
}
