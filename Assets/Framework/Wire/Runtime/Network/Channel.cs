using System;
using Scaffold.AutoPacker;
using Unity.Collections;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// Non-generic base. <see cref="Dispatcher"/> stores channels in a
    /// <c>Channel[256]</c> indexed by discriminator id; the abstract members let the
    /// dispatcher push/pull events without seeing the concrete type.
    /// </summary>
    public abstract class Channel
    {
        /// <summary>Send an event already known to belong to this channel's type.</summary>
        internal abstract void SendBoxed(WireEvent ev);

        /// <summary>
        /// Read this channel's payload from <paramref name="reader"/>, dispatch to
        /// typed subscribers, and return the event up to the dispatcher for the
        /// <see cref="Dispatcher.OnAnyReceived"/> relay.
        /// </summary>
        internal abstract WireEvent DeliverFromBytes(ref FastBufferReader reader);
    }

    /// <summary>
    /// Type-router + serializer for a single <typeparamref name="T"/> on the wire.
    /// Owns its discriminator id, a cached payload size, and the handler reference.
    /// </summary>
    public sealed class Channel<T> : Channel where T : WireEvent, new()
    {
        readonly byte _id;
        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly int _sizeWithDisc;

        /// <summary>Fired when an event of type <typeparamref name="T"/> arrives.</summary>
        public event Action<T> OnReceived;

        internal Channel(byte id, Dispatcher dispatcher, IPackingHandler handler)
        {
            _id = id;
            _dispatcher = dispatcher;
            _handler = handler;

            // Probe PackedSize once via a throwaway instance — the virtual property
            // on WireEvent<TPacked> resolves to sizeof(TPacked), constant per closed type.
            var probe = new T();
            _sizeWithDisc = 1 + probe.PackedSize;
        }

        /// <summary>Direct typed send. Allocates a per-call <see cref="FastBufferWriter"/> against <see cref="Allocator.Temp"/>.</summary>
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
