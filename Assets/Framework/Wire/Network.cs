using System;
using System.Collections.Generic;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Network : IDispatcher
    {
        readonly Dictionary<Type, Channel> _channels = new();
        readonly Queue<Envelope> _inbound = new();
        readonly IPackingHandler _handler;

        public event Action<WireEvent> OnReceived;

        public Network(IPackingHandler handler = null) => _handler = handler;

        IPackingHandler IDispatcher.Handler => _handler;
        void IDispatcher.Receive(WireEvent ev) => OnReceived?.Invoke(ev);

        public Channel<T> Register<T>() where T : WireEvent, new()
        {
            var ch = new Channel<T>(this);
            _channels[typeof(T)] = ch;
            return ch;
        }

        public void Send(WireEvent ev) =>
            _inbound.Enqueue(new Envelope(ev.GetType(), ev.Pack(_handler)));

        public void Pump()
        {
            while (_inbound.Count > 0)
            {
                var env = _inbound.Dequeue();
                _channels[env.EventType].Deliver(env.Packed);
            }
        }

        readonly struct Envelope
        {
            public readonly Type EventType;
            public readonly IPackedStruct Packed;

            public Envelope(Type eventType, IPackedStruct packed)
            {
                EventType = eventType;
                Packed = packed;
            }
        }
    }
}
