#nullable enable
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Mirror-side entity: stores <see cref="TagValue"/> raw (no decode at
    /// apply time), decodes on read via the codec keyed off
    /// <see cref="TagKey{T}"/>. Symmetric with how the source side emits.
    /// </summary>
    public sealed class MirrorEntity
    {
        public long Id { get; }
        public Dictionary<int, TagValue> Tags { get; } = new();
        public MirrorEntity(long id) { Id = id; }

        public T Get<T>(TagKey<T> key)
        {
            if (!Tags.TryGetValue(key.Slot, out var wire))
                return default!;
            return TagCodecs.Get(key).Decode(wire);
        }

        public bool Has(int slot) => Tags.ContainsKey(slot);
    }
}
