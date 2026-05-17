#nullable enable
using System;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Non-generic base — lets the engine store mixed-type keys in a dict
    /// keyed by slot and forward to a type-erased codec without
    /// over-constraining generics on the call site.
    /// </summary>
    public abstract class TagKey
    {
        public int    Slot      { get; }
        public string Name      { get; }
        public Type   ValueType { get; }

        protected TagKey(int slot, string name, Type valueType)
        { Slot = slot; Name = name; ValueType = valueType; }

        public override string ToString() => $"Tag({Name}#{Slot}:{ValueType.Name})";
    }

    /// <summary>
    /// Typed tag handle. <c>T</c> is what authors get back from
    /// <see cref="Entity.Get{T}"/> and <see cref="MirrorEntity.Get{T}"/> — it
    /// drives codec lookup at decode time.
    /// </summary>
    public sealed class TagKey<T> : TagKey
    {
        internal TagKey(int slot, string name) : base(slot, name, typeof(T)) { }
    }
}
