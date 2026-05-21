using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerReadyChangedEvent : WireEvent<PeerReadyChangedEvent.Packed>
    {
        [Packed] public int Seat;
        [Packed] public bool Ready;
    }
}
