using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class BlockStartEvent : WireEvent
    {
        [Packed] public int  Id;
        [Packed] public int  ParentId;
        [Packed] public byte Kind;
        [Packed] public int  SourceId;
    }
}
