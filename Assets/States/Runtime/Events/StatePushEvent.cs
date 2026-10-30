using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.States
{
    public partial class StatePushEvent : WireEvent
    {
        [Packed] public int Slot;
    }
}
