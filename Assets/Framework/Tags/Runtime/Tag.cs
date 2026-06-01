using System;
using Framework.Core;

namespace Framework.Tags
{
    public abstract record Tag(int Slot) : TypeKey(Slot)
    {
        public abstract Type ValueType { get; }
    }

    public abstract record Tag<T>(int Slot) : Tag(Slot) where T : unmanaged
    {
        public override Type ValueType => typeof(T);
    }
}
