using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PlayerDataEvent : WireEvent
    {
        [Packed] public int Seat;
        [Packed] public ulong NetworkId;
        [Packed] public ulong PlayerId;
    }
}
