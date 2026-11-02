using Framework.Wire;

namespace Framework.Blocks
{
    // One line of the game log. Each end sets Position on append, so it is not on the wire.
    public abstract partial class Block : WireEvent
    {
        public int Position { get; internal set; }
        public BlockId Id => new BlockId { Value = Position };
    }
}
