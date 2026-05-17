#nullable enable
using System;

namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Non-generic base for tag keys. Allows the engine to handle keys
    /// uniformly without knowing their value type. Identity is the
    /// <see cref="Slot"/> int — author-assigned at declaration,
    /// registry-enforced unique. <see cref="Name"/> is the wire label
    /// (used by <c>TagSetEvent.Key</c> / <c>TagChangeEvent.Key</c> / etc).
    /// Equality is reference equality.
    /// </summary>
    public abstract class TagKey
    {
        public int Slot { get; }
        public string Name { get; }
        public abstract Type ValueType { get; }

        protected TagKey(int slot, string name)
        {
            Slot = slot;
            Name = name;
            TagRegistry.RegisterInstance(this);
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Typed tag key. <c>Get</c> / <c>Set</c> on an <c>Entity</c> are
    /// strongly typed against <typeparamref name="T"/>. Declare one
    /// static <see cref="TagKey{T}"/> per logical tag (Pattern A) — the
    /// slot is the runtime address, the name is the wire label.
    ///
    /// <para>For keywords that double as event payload classes
    /// (Deathrattle, future Poison), use the Pattern B path:
    /// <c>[TagSlot(N)] class Keyword : ITagKey&lt;bool&gt;</c>. The
    /// registry synthesizes the <c>TagKey&lt;bool&gt;</c> at the
    /// declared slot; access it via <c>Tag&lt;Keyword&gt;.Key</c>.</para>
    /// </summary>
    public sealed class TagKey<T> : TagKey
    {
        public TagKey(int slot, string name) : base(slot, name) { }
        public override Type ValueType => typeof(T);
    }
}
