using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Multiplayer.PlayTests
{
    public partial class ProbeEvent : WireEvent<ProbeEvent.Packed>
    {
        [Packed] public int Value;
    }
}
