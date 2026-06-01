using System;
using Framework.Core;

namespace Framework.Tags
{
    // A tag key — a Registry member over TypeKey that also carries an irreducible value type.
    // Identity is (record type, Slot); ValueType is the stored value's type. The wire carries
    // only the int Slot (the per-T event channel already fixes T), and receivers store the
    // value inline by T. Registration is explicit at the composition root — no ctor side-effect.
    // Games declare their own keys as records: `sealed record Power() : Tag<int>(10);`.
    public abstract record Tag(int Slot) : TypeKey(Slot)
    {
        public abstract Type ValueType { get; }
    }

    public abstract record Tag<T>(int Slot) : Tag(Slot) where T : unmanaged
    {
        public override Type ValueType => typeof(T);
    }
}
