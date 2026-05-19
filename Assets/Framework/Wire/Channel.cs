using System;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    internal interface IDispatcher
    {
        IPackingHandler Handler { get; }
        void Receive(WireEvent ev);
    }

    public abstract class Channel
    {
        internal abstract void Deliver(IPackedStruct packed);
    }

    public sealed class Channel<T> : Channel where T : WireEvent, new()
    {
        readonly IDispatcher _dispatcher;

        public event Action<T> OnReceived;

        internal Channel(IDispatcher dispatcher) => _dispatcher = dispatcher;

        internal override void Deliver(IPackedStruct packed)
        {
            var ev = new T();
            ev.Unpack(packed, _dispatcher.Handler);
            OnReceived?.Invoke(ev);
            _dispatcher.Receive(ev);
        }
    }
}
