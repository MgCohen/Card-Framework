using System;
using Scaffold.AutoPacker;
using Unity.Collections;
using Unity.Netcode;

namespace Framework.Wire
{
    public abstract class Channel
    {
        internal abstract void SendBoxed(WireEvent ev);
        internal abstract WireEvent DeliverFromBytes(ref FastBufferReader reader);
    }

    public sealed class Channel<T> : Channel where T : WireEvent, new()
    {
        readonly byte _id;
        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly int _sizeWithDisc;

        public event Action<T> OnReceived;

        internal Channel(byte id, Dispatcher dispatcher, IPackingHandler handler)
        {
            _id = id;
            _dispatcher = dispatcher;
            _handler = handler;

            // PackedSize is constant per closed type — probe once via throwaway instance.
            var probe = new T();
            _sizeWithDisc = 1 + probe.PackedSize;
        }

        public void Send(T ev)
        {
            var writer = new FastBufferWriter(_sizeWithDisc, Allocator.Temp);
            try
            {
                writer.WriteByteSafe(_id);
                ev.WriteToNetwork(ref writer, _handler);
                _dispatcher.Send(in writer);
            }
            finally
            {
                writer.Dispose();
            }
        }

        internal override void SendBoxed(WireEvent ev) => Send((T)ev);

        internal override WireEvent DeliverFromBytes(ref FastBufferReader reader)
        {
            var ev = new T();
            ev.ReadFromNetwork(ref reader, _handler);
            OnReceived?.Invoke(ev);
            return ev;
        }
    }
}
