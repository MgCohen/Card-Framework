#nullable enable
using System;
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Global per-slot codec registry. Populated at startup either by a
    /// manual installer (today) or by source-gen output (when the generator
    /// project lands). Lookup is by <see cref="TagKey.Slot"/> on the hot
    /// path — no reflection.
    /// </summary>
    public static class TagCodecs
    {
        private static readonly Dictionary<int, ITagCodec> _bySlot = new();

        public static void Register<T>(TagKey<T> key, ITagCodec<T> codec)
            => _bySlot[key.Slot] = codec;

        public static ITagCodec<T> Get<T>(TagKey<T> key)
        {
            if (!_bySlot.TryGetValue(key.Slot, out var codec))
                throw new InvalidOperationException($"No codec registered for {key}.");
            return (ITagCodec<T>)codec;
        }

        public static ITagCodec GetUntyped(int slot)
        {
            if (!_bySlot.TryGetValue(slot, out var codec))
                throw new InvalidOperationException($"No codec registered for slot {slot}.");
            return codec;
        }

        /// <summary>
        /// Type-erased encode — the path <see cref="EntityStore"/> uses when
        /// reacting to an <see cref="Entity"/>'s internal tag-change event.
        /// </summary>
        public static TagValue Encode(TagKey key, object value)
            => GetUntyped(key.Slot).EncodeObject(value);

        public static bool IsRegistered(int slot) => _bySlot.ContainsKey(slot);
    }
}
