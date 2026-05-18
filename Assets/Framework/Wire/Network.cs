using System;
using System.Collections.Generic;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Network
    {
        readonly Dictionary<Type, Action<IPackedStruct>> _deliverers = new();
        readonly Queue<(Action<IPackedStruct> deliver, IPackedStruct packed)> _inbound = new();

        public event Action<WireEvent> OnReceived;

        public Channel<T> Register<T>() where T : WireEvent, IPackable, IUnpackable, new()
        {
            var ch = new Channel<T>();
            ch.OnDelivered += ev => OnReceived?.Invoke(ev);
            _deliverers[typeof(T)] = ch.Deliver;
            return ch;
        }

        public void Send(WireEvent ev) =>
            _inbound.Enqueue((_deliverers[ev.GetType()], ((IPackable)ev).Pack()));

        public void Pump()
        {
            while (_inbound.Count > 0)
            {
                var (deliver, packed) = _inbound.Dequeue();
                deliver(packed);
            }
        }
    }
}
