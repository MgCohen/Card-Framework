using System;
using System.Collections.Generic;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Network
    {
        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly Dictionary<Type, NetworkChannel> _byType = new();
        byte _nextId;

        public event Action<WireEvent> OnReceived;

        public Network(ITransport transport, IPackingHandler handler = null)
        {
            _handler = handler;
            _dispatcher = new Dispatcher(transport);
            _dispatcher.OnAnyReceived += ev => OnReceived?.Invoke(ev);
        }

        public NetworkChannel<T> Register<T>() where T : WireEvent, new()
        {
            var id = _nextId++;
            var ch = new NetworkChannel<T>(id, _dispatcher, _handler);
            _byType[typeof(T)] = ch;
            _dispatcher.Register(id, ch);
            return ch;
        }

        public void Send(WireEvent ev) => _byType[ev.GetType()].SendBoxed(ev);
    }
}
