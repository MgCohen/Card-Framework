using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerLeftEvent : WireEvent
    {
        [Packed] public int Seat;
    }
}
