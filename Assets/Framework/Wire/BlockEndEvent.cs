using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockEndEvent : WireEvent
    {
        [Packed] public byte Kind;
    }
}
