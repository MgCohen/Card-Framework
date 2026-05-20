using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Session.PlayTests
{
    public partial class ProbeEvent : WireEvent<ProbeEvent.Packed>
    {
        [Packed] public int Value;
    }
}
