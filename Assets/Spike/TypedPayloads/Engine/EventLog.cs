#nullable enable
using System.Collections;
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Append-only ordered log of wire events. The spike's stand-in for the
    /// real transport — what gets serialized when transport lands is exactly
    /// what's appended here.
    /// </summary>
    public sealed class EventLog : IReadOnlyList<WireEvent>
    {
        private readonly List<WireEvent> _events = new();

        public void Append(WireEvent ev) => _events.Add(ev);

        public WireEvent this[int index] => _events[index];
        public int       Count           => _events.Count;
        public IEnumerator<WireEvent> GetEnumerator() => _events.GetEnumerator();
        IEnumerator     IEnumerable.GetEnumerator()   => GetEnumerator();
    }
}
