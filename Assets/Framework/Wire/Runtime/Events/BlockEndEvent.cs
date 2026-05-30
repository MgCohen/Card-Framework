using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockEndEvent : WireEvent<BlockEndEvent.Packed>
    {
        [Packed] public int  Id;
        [Packed] public byte Outcome;
    }
}
