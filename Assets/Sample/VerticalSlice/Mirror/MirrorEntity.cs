#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.Mirror
{
    /// <summary>
    /// Mirror-side projection of an entity. No type name — there are no
    /// entity subclasses on the primary side either; "kind" is just a tag.
    /// The mirror reconstructs entities from the wire stream; it never
    /// references the primary <c>Entity</c> type.
    /// </summary>
    public sealed class MirrorEntity
    {
        public int Id { get; }
        public Dictionary<string, object?> Tags { get; } = new();

        public MirrorEntity(int id) { Id = id; }

        public override string ToString() => $"#{Id}";
    }
}
