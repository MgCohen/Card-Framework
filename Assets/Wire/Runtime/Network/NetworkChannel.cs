using System;
using Scaffold.AutoPacker;
using Unity.Collections;
using Unity.Netcode;

namespace Framework.Wire
{
    public abstract class NetworkChannel
    {
        internal abstract void SendBoxed(WireEvent ev);
        internal abstract WireEvent DeliverFromBytes(ref FastBufferReader reader);
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

        public override unsafe void Send(T ev)
        {
            var writer = new FastBufferWriter(_sizeWithDisc, Allocator.Temp);
            try
            {
                writer.WriteByteSafe(_id);
                var packed = ev.PackTyped(_handler);
                writer.WriteBytesSafe((byte*)&packed, sizeof(TPacked));
                _dispatcher.Send(in writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        internal override void SendBoxed(WireEvent ev) => Send((T)ev);

        internal override unsafe WireEvent DeliverFromBytes(ref FastBufferReader reader)
        {
            TPacked packed;
            reader.ReadBytesSafe((byte*)&packed, sizeof(TPacked));

            var ev = new T();
            ev.UnpackTyped(packed, _handler);
            RaiseReceived(ev);
            return ev;
        }
    }
}
