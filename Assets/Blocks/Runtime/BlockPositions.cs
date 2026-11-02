using Framework.Wire;

namespace Framework.Blocks
{
    // Sets Position in OnAppending, so every OnAppended listener reads it.
    public sealed class BlockPositions
    {
        public BlockPositions(EventStream stream)
        {
            stream.OnAppending += ev =>
            {
                if (ev is Block block) block.Position = stream.Count;
            };
        }
    }
}
