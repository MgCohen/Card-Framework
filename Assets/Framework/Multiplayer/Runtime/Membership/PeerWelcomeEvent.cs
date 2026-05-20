using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer
{
    public partial class PeerWelcomeEvent : WireEvent<PeerWelcomeEvent.Packed>
    {
        [Packed] public int YourSeat;
        [Packed] public ulong YourNetworkId;
    }
}
