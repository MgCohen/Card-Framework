#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Mirror
{
    /// <summary>
    /// Cursor over the event log. <c>GoTo(N)</c> walks forward via
    /// <c>Apply</c> or backward via <c>Unapply</c>, leaving the mirror
    /// in the exact state implied by "events 0..N-1 have been applied."
    /// </summary>
    public sealed class ScrubController
    {
        private readonly MirrorWorld _mirror;
        private readonly IReadOnlyList<WireEvent> _log;
        private int _cursor;

        public int Cursor => _cursor;

        public ScrubController(MirrorWorld mirror, IReadOnlyList<WireEvent> log)
        {
            _mirror = mirror;
            _log = log;
        }

        public void GoTo(int target)
        {
            target = Math.Clamp(target, 0, _log.Count);

            while (_cursor < target)
            {
                _mirror.Apply(_log[_cursor]);
                _cursor++;
            }
            while (_cursor > target)
            {
                _cursor--;
                _mirror.Unapply(_log[_cursor]);
            }
        }
    }
}
