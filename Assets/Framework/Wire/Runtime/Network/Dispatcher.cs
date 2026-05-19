using System;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// Send/receive engine. Owns the <see cref="ITransport"/> and the per-id channel
    /// registry. Channels call <see cref="Send"/> after they've written their bytes;
    /// the transport's inbound callback comes back through <see cref="OnTransportReceived"/>,
    /// which reads the discriminator and routes to the matching channel.
    /// </summary>
    /// <remarks>
    /// Sub-service of <see cref="Network"/>. Network builds the Dispatcher, injects it
    /// into channels at registration, and relays <see cref="OnAnyReceived"/> upward.
    /// Dependencies flow strictly downward: <c>Network → Channel → Dispatcher → ITransport</c>
    /// on send; <c>ITransport → Dispatcher → Channel</c> on receive. No back-references.
    /// </remarks>
    public sealed class Dispatcher
    {
        readonly ITransport _transport;
        readonly Channel[] _channels = new Channel[256];

        /// <summary>Fired after a received packet has been dispatched to its channel.</summary>
        public event Action<WireEvent> OnAnyReceived;

        public Dispatcher(ITransport transport)
        {
            _transport = transport;
            _transport.OnReceived += OnTransportReceived;
        }

        internal void Register(byte id, Channel channel) => _channels[id] = channel;

        /// <summary>
        /// Hand a channel-written packet to the transport. The channel is responsible for
        /// writing the discriminator byte + payload into the buffer before calling this.
        /// </summary>
        internal void Send(in FastBufferWriter writer) => _transport.Send(in writer);

        void OnTransportReceived(FastBufferReader reader)
        {
            reader.ReadByteSafe(out var id);
            var ev = _channels[id].DeliverFromBytes(ref reader);
            OnAnyReceived?.Invoke(ev);
        }
    }
}
