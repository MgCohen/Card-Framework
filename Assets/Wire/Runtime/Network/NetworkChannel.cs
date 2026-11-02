using System;
using System.Runtime.InteropServices;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public abstract class NetworkChannel
    {
        internal abstract void SendBoxed(WireEvent ev);
        internal abstract WireEvent DeliverFromBytes(ReadOnlySpan<byte> payload);
    }

    public abstract class NetworkChannel<T> : NetworkChannel where T : WireEvent, new()
    {
        public event Action<T> OnReceived;
        protected void RaiseReceived(T ev) => OnReceived?.Invoke(ev);

        public abstract void Send(T ev);
    }

    public sealed class NetworkChannel<T, TPacked> : NetworkChannel<T>
        where T : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>, new()
        where TPacked : unmanaged, IPackedStruct
    {
        readonly byte _id;
        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly int _sizeWithDisc;

        internal NetworkChannel(byte id, Dispatcher dispatcher, IPackingHandler handler)
        {
            _id = id;
            _dispatcher = dispatcher;
            _handler = handler;

            // sizeof(TPacked) is a compile-time constant on the closed concrete.
            unsafe { _sizeWithDisc = 1 + sizeof(TPacked); }
        }

        public override void Send(T ev)
        {
            Span<byte> frame = stackalloc byte[_sizeWithDisc];
            frame[0] = _id;
            var packed = ev.PackTyped(_handler);
            MemoryMarshal.Write(frame.Slice(1), ref packed);
            _dispatcher.Send(frame);
        }

        internal override void SendBoxed(WireEvent ev) => Send((T)ev);

        internal override WireEvent DeliverFromBytes(ReadOnlySpan<byte> payload)
        {
            var packed = MemoryMarshal.Read<TPacked>(payload);   // throws if payload is short

            var ev = new T();
            ev.UnpackTyped(packed, _handler);
            RaiseReceived(ev);
            return ev;
        }
    }
}
