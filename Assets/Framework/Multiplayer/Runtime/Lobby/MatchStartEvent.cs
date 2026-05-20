using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    // Host → all: transition the session into InMatch. The act of arriving is the signal;
    // the reserved byte is a placeholder for future payload (session id, deterministic seed,
    // server start time) and to give AutoPacker something to pack.
    public partial class MatchStartEvent : WireEvent<MatchStartEvent.Packed>
    {
        [Packed] public byte Reserved;
    }
}
