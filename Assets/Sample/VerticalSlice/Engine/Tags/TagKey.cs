#nullable enable
using System;

namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Non-generic base for tag keys. Allows the engine to handle keys
    /// uniformly without knowing their value type. Keys are singletons —
    /// declare one static <see cref="TagKey{T}"/> per logical tag.
    /// Equality is reference equality (do not override).
    /// </summary>
    public abstract class TagKey
    {
        public string Name { get; }
        public int Slot { get; }
        public abstract Type ValueType { get; }

        protected TagKey(string name)
        {
            Name = name;
            Slot = TagRegistry.Register(this);
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Typed tag key. <c>Get</c>/<c>Set</c> on an <c>Entity</c> are
    /// strongly typed against <typeparamref name="T"/>.
    /// </summary>
    public sealed class TagKey<T> : TagKey
    {
        public TagKey(string name) : base(name) { }
        public override Type ValueType => typeof(T);
    }
}
