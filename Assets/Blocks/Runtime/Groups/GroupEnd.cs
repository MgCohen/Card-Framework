using Scaffold.AutoPacker;

namespace Framework.Blocks
{
    public sealed partial class GroupEnd : Block
    {
        [Packed] public BlockId StartId;
        [Packed] public byte    Outcome;
    }
}
