using System;
using System.Collections.Generic;

namespace Framework.Wire
{
    public sealed class EventStream : IEventEmitter
    {
        readonly List<WireEvent> _events = new();

        // Raised before OnAppended, with the same event. Use it to set data that OnAppended listeners read.
        public event Action<WireEvent> OnAppending;
        public event Action<WireEvent> OnAppended;

        public int Count => _events.Count;
        public WireEvent this[int index] => _events[index];

        public void Emit(WireEvent ev)
        {
            _events.Add(ev);
            OnAppending?.Invoke(ev);
            OnAppended?.Invoke(ev);
        }
    }
}
