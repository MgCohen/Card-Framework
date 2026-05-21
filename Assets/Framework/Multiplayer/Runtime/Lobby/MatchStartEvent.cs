using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    // No semantic payload — MatchStart is a transition signal.
    // AutoPacker codegen requires at least one [Packed] field, so this placeholder
    // exists solely to make the generated Packed struct valid. Replace with a real
    // field (e.g. MatchId, Seed) when one becomes useful; do not remove without one.
    public partial class MatchStartEvent : WireEvent<MatchStartEvent.Packed>
    {
        [Packed] public byte Reserved;
    }
}
