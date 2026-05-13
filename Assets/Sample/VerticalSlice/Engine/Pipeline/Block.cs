#nullable enable
using VerticalSlice.Engine.Commands;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// In-memory record of an open block. The wire form is the pair
    /// <c>BlockStartEvent</c> / <c>BlockEndEvent</c>; this struct exists
    /// so the <c>World</c> can track the nesting stack and route writes.
    /// </summary>
    public sealed class Block
    {
        public BlockType Type { get; }
        public Command? Cause { get; }   // null only for BlockType.System
        public Block? Parent { get; }

        public Block(BlockType type, Command? cause, Block? parent)
        {
            Type = type;
            Cause = cause;
            Parent = parent;
        }
    }
}
