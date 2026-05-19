using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public sealed class Dispatcher
    {
        readonly ITransport _transport;
        readonly Channel[] _channels = new Channel[256];

        public event Action<WireEvent> OnAnyReceived;

        public Dispatcher(ITransport transport)
        {
            _transport = transport;
            _transport.OnReceived += OnTransportReceived;
        }

        internal void Register(byte id, Channel channel) => _channels[id] = channel;

        internal void Send(in FastBufferWriter writer) => _transport.Send(in writer);

        void OnTransportReceived(FastBufferReader reader)
        {
            reader.ReadByteSafe(out var id);
            var ev = _channels[id].DeliverFromBytes(ref reader);
            OnAnyReceived?.Invoke(ev);
        }
    }
}
