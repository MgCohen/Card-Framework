#nullable enable
using System;

namespace Spike.Modifiers
{
    public abstract class TagKey
    {
        public int    Slot { get; }
        public string Name { get; }
        public abstract Type ValueType { get; }

        protected TagKey(int slot, string name) { Slot = slot; Name = name; }
        public override string ToString() => Name;
    }

    public sealed class TagKey<T> : TagKey
    {
        public TagKey(int slot, string name) : base(slot, name) { }
        public override Type ValueType => typeof(T);
    }
}
