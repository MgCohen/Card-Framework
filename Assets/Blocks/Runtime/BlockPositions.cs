using Framework.Wire;

namespace Framework.Blocks
{
    // Subscribe before all other listeners, so they read a set Position.
    public sealed class BlockPositions
    {
        public BlockPositions(EventStream stream)
        {
            stream.OnAppended += ev =>
            {
                if (ev is Block block) block.Position = stream.Count;
            };
        }
    }
}
