using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class MatchStartEvent : WireEvent
    {
        [Packed] public byte Reserved; // AutoPacker requires >=1 [Packed] field; placeholder, do not remove
    }
}
