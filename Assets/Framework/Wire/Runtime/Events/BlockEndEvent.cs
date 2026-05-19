using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockEndEvent : WireEvent<BlockEndEvent.Packed>
    {
        [Packed] public byte Kind;
    }
}
