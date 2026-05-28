using System;

namespace Framework.Tags
{
    public abstract class TagKey
    {
        public int Slot { get; }
        public abstract Type ValueType { get; }

        // Double-dispatch hook for ITagKeyVisitor. Each closed-type override picks Visit<T>
        // at compile time, letting non-generic callers (e.g. iterating TagRegistry.All) drive
        // T-specific work without reflection.
        internal abstract void Accept(ITagKeyVisitor visitor);

        protected TagKey(int slot)
        {
            Slot = slot;
            TagRegistry.RegisterInstance(this);
        }

        public override string ToString() => $"TagKey<{ValueType.Name}>[{Slot}]";
    }

    public sealed class TagKey<T> : TagKey where T : unmanaged
    {
        public TagKey(int slot) : base(slot) { }
        public override Type ValueType => typeof(T);

        internal override void Accept(ITagKeyVisitor visitor) => visitor.Visit(this);
    }
}
