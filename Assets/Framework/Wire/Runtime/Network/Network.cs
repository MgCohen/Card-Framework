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

        // Two type args: T is the leaf event class, TPacked is its AutoPacker-generated
        // nested Packed struct (i.e. T.Packed). The pair is required exactly here — every
        // downstream surface (Send(ev), the OnReceived event, the returned NetworkChannel<T>)
        // hides TPacked behind the single-generic NetworkChannel<T>.
        public NetworkChannel<T> Register<T, TPacked>()
            where T : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>, new()
            where TPacked : unmanaged, IPackedStruct
        {
            var id = _nextId++;
            var ch = new NetworkChannel<T, TPacked>(id, _dispatcher, _handler);
            _byType[typeof(T)] = ch;
            _dispatcher.Register(id, ch);
            return ch;
        }

        public void Send(WireEvent ev) => _byType[ev.GetType()].SendBoxed(ev);
    }
}
