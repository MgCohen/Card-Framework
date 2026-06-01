using System;
using System.Collections.Generic;

namespace Framework.Wire
{
    public sealed class EventStream : IEventEmitter
    {
        readonly List<WireEvent> _events = new();

        public event Action<WireEvent> OnAppended;

        public int Count => _events.Count;
        public WireEvent this[int index] => _events[index];

        public void Emit(WireEvent ev)
        {
            _events.Add(ev);
            OnAppended?.Invoke(ev);
        }
    }
}
