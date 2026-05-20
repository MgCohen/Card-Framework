using System.Collections.Generic;

namespace Framework.Wire
{
    internal sealed class HandlerBucket<T> : IHandlerBucket where T : WireEvent
    {
        readonly List<IWireEventHandler<T>> _handlers = new();

        public void Add(IWireEventHandler<T> handler) => _handlers.Add(handler);

        public void Dispatch(T ev)
        {
            for (var i = 0; i < _handlers.Count; i++)
                _handlers[i].Apply(ev);
        }

        public void DispatchUntyped(WireEvent ev) => Dispatch((T)ev);
    }
}
