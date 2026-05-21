using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class MatchStartEvent : WireEvent<MatchStartEvent.Packed>
    {
        [Packed] public byte Reserved;
    }
}
