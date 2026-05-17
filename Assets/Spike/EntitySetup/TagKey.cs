#nullable enable
using System;

namespace Spike.EntitySetup
{
    /// <summary>
    /// Non-generic base — lets the store reason about keys uniformly when
    /// it doesn't care about the value type. Slot is the runtime address.
    /// </summary>
    public abstract class TagKey
    {
        public int    Slot { get; }
        public string Name { get; }
        public abstract Type ValueType { get; }

        protected TagKey(int slot, string name) { Slot = slot; Name = name; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Typed tag key. <c>Get</c> / <c>Set</c> on an <see cref="Entity"/>
    /// are strongly typed against <typeparamref name="T"/>. Slot is
    /// author-assigned — the spike trusts authors not to collide; a real
    /// implementation would add a registry guard (see the VerticalSlice
    /// TagRegistry for that pattern).
    /// </summary>
    public sealed class TagKey<T> : TagKey
    {
        public TagKey(int slot, string name) : base(slot, name) { }
        public override Type ValueType => typeof(T);
    }
}
