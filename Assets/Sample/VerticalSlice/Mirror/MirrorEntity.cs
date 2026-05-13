#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.Mirror
{
    /// <summary>
    /// Mirror-side projection of an entity. No behaviour — just an id,
    /// a type name (for display), and a tag dictionary keyed by wire-name.
    /// The mirror reconstructs entities from the wire stream alone; it
    /// never references the primary <c>World</c>'s <c>Entity</c> types.
    /// </summary>
    public sealed class MirrorEntity
    {
        public int Id { get; }
        public string TypeName { get; }
        public Dictionary<string, object?> Tags { get; } = new();

        public MirrorEntity(int id, string typeName)
        {
            Id = id;
            TypeName = typeName;
        }

        public override string ToString() => $"{TypeName}#{Id}";
    }
}
