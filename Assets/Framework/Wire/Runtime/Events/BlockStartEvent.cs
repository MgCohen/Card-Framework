using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockStartEvent : WireEvent<BlockStartEvent.Packed>
    {
        [Packed] public byte Kind;
    }
}
