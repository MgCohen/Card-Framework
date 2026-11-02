using Framework.Wire;

namespace Framework.Blocks
{
    // Position is set on append. It is not on the wire.
    public abstract class Block : WireEvent
    {
        public int Position { get; internal set; }
        public BlockId Id => new BlockId { Value = Position };
    }
}
