using System;
using System.Collections.Generic;

namespace Framework.Wire
{
    public sealed class EventStream
    {
        readonly List<WireEvent> _events = new();

        public event Action<WireEvent> OnAppended;

        public int Count => _events.Count;
        public WireEvent Get(int index) => _events[index];

        public void Add(WireEvent ev)
        {
            _events.Add(ev);
            OnAppended?.Invoke(ev);
        }
    }
}
