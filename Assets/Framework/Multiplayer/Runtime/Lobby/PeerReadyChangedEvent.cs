using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerReadyChangedEvent : WireEvent
    {
        [Packed] public int Seat;
        [Packed] public bool Ready;
    }
}
