using System;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Channel<T> where T : WireEvent, IPackable, IUnpackable, new()
    {
        public event Action<T> OnReceived;
        internal event Action<WireEvent> OnDelivered;

        internal Channel() { }

        internal void Deliver(IPackedStruct packed)
        {
            var ev = new T();
            ev.Unpack(packed);
            OnReceived?.Invoke(ev);
            OnDelivered?.Invoke(ev);
        }
    }
}
