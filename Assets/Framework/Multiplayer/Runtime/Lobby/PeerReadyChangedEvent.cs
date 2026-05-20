using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    // Bi-directional ready signal. Client → host carries the submitter's seat and the
    // new ready value; host → all carries the same shape (host self-echoes its own
    // submission and broadcasts other clients' submissions). Both directions share one
    // event type — the receive handler is idempotent against self-echo (already in
    // _ready with the same value is a no-op).
    public partial class PeerReadyChangedEvent : WireEvent<PeerReadyChangedEvent.Packed>
    {
        [Packed] public int Seat;
        [Packed] public bool Ready;
    }
}
