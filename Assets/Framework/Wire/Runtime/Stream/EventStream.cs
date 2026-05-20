using System;
using System.Collections.Generic;

namespace Framework.Wire
{
    public sealed class EventStream : IEventEmitter, IHandlerRegistry
    {
        readonly List<WireEvent> _events = new();
        readonly Dictionary<Type, IHandlerBucket> _buckets = new();

        public event Action<WireEvent> OnAppended;

        public int Count => _events.Count;
        public WireEvent this[int index] => _events[index];

        public void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent
        {
            if (!_buckets.TryGetValue(typeof(TEvent), out var bucket))
                _buckets[typeof(TEvent)] = bucket = new HandlerBucket<TEvent>();
            ((HandlerBucket<TEvent>)bucket).Add(handler);
        }

        public void Emit<TEvent>(TEvent ev) where TEvent : WireEvent
        {
            _events.Add(ev);
            OnAppended?.Invoke(ev);
            if (_buckets.TryGetValue(typeof(TEvent), out var bucket))
                ((HandlerBucket<TEvent>)bucket).Dispatch(ev);
        }

        public void Emit(WireEvent ev)
        {
            _events.Add(ev);
            OnAppended?.Invoke(ev);
            if (_buckets.TryGetValue(ev.GetType(), out var bucket))
                bucket.DispatchUntyped(ev);
        }
    }
}
