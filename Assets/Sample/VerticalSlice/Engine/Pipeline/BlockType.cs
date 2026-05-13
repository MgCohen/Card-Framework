#nullable enable
namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Typed identifier for block kinds. Not a C# enum — games can declare
    /// new block types as additional static fields without modifying the
    /// framework. Equality is reference equality.
    /// </summary>
    public sealed class BlockType
    {
        public string Name { get; }
        private BlockType(string name) { Name = name; }

        public static readonly BlockType System  = new("System");
        public static readonly BlockType Play    = new("Play");
        public static readonly BlockType Power   = new("Power");
        public static readonly BlockType Trigger = new("Trigger");
        public static readonly BlockType Attack  = new("Attack");
        public static readonly BlockType Deaths  = new("Deaths");

        public override string ToString() => Name;
    }
}
