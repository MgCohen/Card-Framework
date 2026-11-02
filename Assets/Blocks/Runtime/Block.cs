using Framework.TypeRouting;
using Framework.Wire;

namespace Framework.Blocks
{
    // Position is set on append. It is not on the wire.
    // Not partial: AutoPack sees an abstract partial type with no [Packed] member as a second root.
    // If you add a [Packed] field, make the class partial.
    // RequireDerived keeps each block type in a stripped player, because channel ids depend on the full set.
    [RequireDerived]
    public abstract class Block : WireEvent
    {
        public int Position { get; internal set; }
        public BlockId Id => new BlockId { Value = Position };
    }
}
