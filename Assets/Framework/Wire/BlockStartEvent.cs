using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockStartEvent : WireEvent
    {
        [Packed] public byte Kind;
    }
}
