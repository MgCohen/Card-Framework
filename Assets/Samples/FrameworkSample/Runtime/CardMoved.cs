using Framework.Blocks;
using Scaffold.AutoPacker;

namespace Framework.Samples.FrameworkSample
{
    public sealed partial class CardMoved : Block
    {
        [Packed] public CardId Card;
        [Packed] public int    From;
        [Packed] public int    To;
    }
}
