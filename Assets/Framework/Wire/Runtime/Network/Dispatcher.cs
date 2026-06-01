using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public sealed class Dispatcher
    {
        internal const int Capacity = 256;   // wire ids are a byte; this bounds the channel table

        readonly ITransport _transport;
        readonly NetworkChannel[] _channels = new NetworkChannel[Capacity];

        public event Action<WireEvent> OnAnyReceived;

        public Dispatcher(ITransport transport)
        {
            _transport = transport;
            _transport.OnReceived += OnTransportReceived;
        }

        internal void Register(byte id, NetworkChannel channel) => _channels[id] = channel;

        internal void Send(in FastBufferWriter writer) => _transport.Send(in writer);

        void OnTransportReceived(FastBufferReader reader)
        {
            reader.ReadByteSafe(out var id);
            var channel = _channels[id]
                ?? throw new InvalidOperationException($"No wire channel registered for id {id}.");
            var ev = channel.DeliverFromBytes(ref reader);
            OnAnyReceived?.Invoke(ev);
        }
    }
}
