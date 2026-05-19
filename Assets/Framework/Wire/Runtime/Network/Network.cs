using System;
using System.Collections.Generic;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    /// <summary>
    /// Orchestrator façade. Composes a <see cref="Dispatcher"/> over an
    /// <see cref="ITransport"/>, owns the per-type channel index, exposes the public
    /// surface (<see cref="Register{T}"/>, <see cref="Send"/>, <see cref="OnReceived"/>).
    /// One <see cref="Network"/> per end of a connection.
    /// </summary>
    public sealed class Network
    {
        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly Dictionary<Type, Channel> _byType = new();
        byte _nextId;

        /// <summary>Fires after every received event has been dispatched to its channel.</summary>
        public event Action<WireEvent> OnReceived;

        public Network(ITransport transport, IPackingHandler handler = null)
        {
            _handler = handler;
            _dispatcher = new Dispatcher(transport);
            _dispatcher.OnAnyReceived += ev => OnReceived?.Invoke(ev);
        }

        /// <summary>
        /// Register a wire-event type. Channel id is assigned by registration order;
        /// both ends must register the same types in the same order for ids to match.
        /// Returns the typed channel for direct subscription / send.
        /// </summary>
        public Channel<T> Register<T>() where T : WireEvent, new()
        {
            var id = _nextId++;
            var ch = new Channel<T>(id, _dispatcher, _handler);
            _byType[typeof(T)] = ch;
            _dispatcher.Register(id, ch);
            return ch;
        }

        /// <summary>Boxed entry point — looks up the channel by runtime type and forwards.</summary>
        public void Send(WireEvent ev) => _byType[ev.GetType()].SendBoxed(ev);
    }
}
