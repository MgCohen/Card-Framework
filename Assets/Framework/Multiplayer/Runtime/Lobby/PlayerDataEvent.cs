using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    // Client → host: "this is my submission." Carries the base PlayerData fields.
    // Subclass extension fields are not encoded here (see PlayerData comment for
    // the polymorphic-wire-event deferral).
    public partial class PlayerDataEvent : WireEvent<PlayerDataEvent.Packed>
    {
        [Packed] public int Seat;             // submitter's seat (Self.Seat)
        [Packed] public ulong NetworkId;
        [Packed] public ulong PlayerId;
    }
}
