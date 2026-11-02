using System;

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

        internal void Send(ReadOnlySpan<byte> frame) => _transport.Send(frame);

        void OnTransportReceived(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty) throw new InvalidOperationException("Empty wire frame.");
            var id = bytes[0];
            var channel = _channels[id]
                ?? throw new InvalidOperationException($"No wire channel registered for id {id}.");
            var ev = channel.DeliverFromBytes(bytes.Slice(1));
            OnAnyReceived?.Invoke(ev);
        }
    }
}
