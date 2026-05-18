#nullable enable
using System.Collections.Generic;

namespace Spike.Modifiers
{
    // Three-field identity + sparse slot bag (current) + sparse base bag.
    //
    // Base is a parallel store of "what the value was when authored content
    // first set it". Rebaseable slots reset to Base on refresh, then replay
    // every modifier's Refresh effect. Transient slots ignore Base entirely.
    //
    // Spawn helpers call SetBase for rebaseable stats; gameplay never does.
    public sealed class Entity
    {
        public int        Id           { get; internal set; }
        public EntityType Type         { get; internal set; }
        public int        DefinitionId { get; internal set; }

        private readonly Dictionary<int, object?> _slots = new();
        private readonly Dictionary<int, object?> _base  = new();

        public T Get<T>(TagKey<T> key)
        {
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T t) return t;
            return default!;
        }
        public bool Has<T>(TagKey<T> key) => _slots.ContainsKey(key.Slot);
        public void Set<T>(TagKey<T> key, T value) => _slots[key.Slot] = value;

        // Mirrors current; also records the base. Spawn-time only by convention.
        public void SetBase<T>(TagKey<T> key, T value)
        {
            _slots[key.Slot] = value;
            _base [key.Slot] = value;
        }

        // Untyped current write — used by the refresh reset pass.
        public void SetSlotRaw(int slot, object? value) => _slots[slot] = value;

        public IReadOnlyDictionary<int, object?> Slots => _slots;
        public IReadOnlyDictionary<int, object?> Base  => _base;

        public override string ToString() => $"Entity#{Id}";
    }
}
