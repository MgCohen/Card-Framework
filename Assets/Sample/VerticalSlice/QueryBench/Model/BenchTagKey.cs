#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.QueryBench.Model
{
    // Slot-based tag identity mirroring production TagKey.
    // Bench-local — not the production type.
    public abstract class BenchTagKey
    {
        private static int _nextSlot = 0;
        private static readonly Dictionary<string, int> _slotsByName = new();

        public int Slot { get; }
        public string Name { get; }

        protected BenchTagKey(string name)
        {
            Name = name;
            if (!_slotsByName.TryGetValue(name, out var slot))
            {
                slot = _nextSlot++;
                _slotsByName[name] = slot;
            }
            Slot = slot;
        }
    }

    public sealed class BenchTagKey<T> : BenchTagKey
    {
        public BenchTagKey(string name) : base(name) { }
    }
}
