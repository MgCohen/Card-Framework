using Scaffold.AutoPacker;

namespace Framework.Blocks.Tests
{
    public sealed partial class TestBlock : Block
    {
        [Packed] public int Value;
    }
}
