using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    // Host → all: resolved authoritative shape for one peer. One event per peer per
    // update. Subclass extension fields not encoded (polymorphism deferred).
    public partial class PlayerContextEvent : WireEvent<PlayerContextEvent.Packed>
    {
        [Packed] public int Seat;
        [Packed] public ulong NetworkId;
        [Packed] public ulong PlayerId;
    }
}
