#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// Abstract base for every state-changing operation. Each command
    /// declares the <see cref="BlockType"/> it opens; <c>Apply</c> is
    /// where the actual tag writes happen.
    ///
    /// Command public properties are walked by reflection at
    /// <c>BlockStart</c> emission time to capture the wire payload —
    /// keep them simple (primitives, Entity refs, value records).
    /// </summary>
    public abstract class Command
    {
        public abstract BlockType BlockType { get; }
        public abstract void Apply(World world);
    }
}
