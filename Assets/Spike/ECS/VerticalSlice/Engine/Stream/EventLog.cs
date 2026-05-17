#nullable enable
using System;
using System.Collections.Generic;

namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// Append-only event log. The single source of truth for the slice's
    /// state stream. <see cref="OnAppend"/> fires synchronously per entry
    /// so subscribers (Mirror, UI) can react incrementally.
    /// </summary>
    public sealed class EventLog
    {
        private readonly List<WireEvent> _events = new();

        public IReadOnlyList<WireEvent> Events => _events;
        public int Count => _events.Count;
        public WireEvent this[int index] => _events[index];

        public event Action<WireEvent>? OnAppend;

        public void Append(WireEvent ev)
        {
            _events.Add(ev);
            OnAppend?.Invoke(ev);
        }
    }
}
