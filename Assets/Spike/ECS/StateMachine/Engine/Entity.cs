#nullable enable
using System.Collections.Generic;

namespace StateMachine.Engine
{
    /// <summary>
    /// Bare entity for the state-machine spike: id + a typed tag bag. Just
    /// enough for "DeclareAttackersState reads world.Cards.With(IsAttacking)"
    /// to be a real query, not a stub.
    /// </summary>
    public sealed class Entity
    {
        public long Id { get; internal set; }

        private readonly Dictionary<int, object?> _slots = new();

        public T Get<T>(TagKey<T> key) =>
            _slots.TryGetValue(key.Slot, out var raw) && raw is T t ? t : default!;

        public bool Has<T>(TagKey<T> key) =>
            _slots.TryGetValue(key.Slot, out var raw) && raw is T;

        public void Set<T>(TagKey<T> key, T value) => _slots[key.Slot] = value;

        public void Remove<T>(TagKey<T> key) => _slots.Remove(key.Slot);

        public override string ToString() => $"Entity#{Id}";
    }
}
