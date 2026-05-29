using System;

namespace Framework.Tags
{
    public abstract class TagKey
    {
        public int Slot { get; }
        public abstract Type ValueType { get; }

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
    }
}
