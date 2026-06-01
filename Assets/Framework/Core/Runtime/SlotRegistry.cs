using System;
using System.Collections.Generic;

namespace Framework.Core
{
    // Family A / Registry: discriminator -> domain identity. A scoped instance (never a static
    // global) so tests and server/client scopes stay isolated. Two authoring paths, both
    // explicit at the composition root:
    //   Register<T>()      — code member; also enables Of<T>() and is-pattern use.
    //   Register(member)   — data member (asset -> instance); identity only, no Of<T>.
    // Reads: BySlot / TryBySlot / Of<T>. Slots are unique — a clash throws at registration.
    public class SlotRegistry<TKey> where TKey : TypeKey
    {
        readonly Dictionary<int, TKey>  _bySlot = new();
        readonly Dictionary<Type, TKey> _byType = new();   // code members only

        public void Register<T>() where T : TKey, new()
        {
            var member = new T();
            Add(member);
            _byType[typeof(T)] = member;
        }

        public void Register(TKey member) => Add(member);

        public TKey BySlot(int slot)                 => _bySlot[slot];
        public bool TryBySlot(int slot, out TKey key) => _bySlot.TryGetValue(slot, out key);
        public T    Of<T>() where T : TKey            => (T)_byType[typeof(T)];

        void Add(TKey member)
        {
            if (_bySlot.TryGetValue(member.Slot, out var clash))
                throw new InvalidOperationException(
                    $"Slot {member.Slot} already claimed by {clash.GetType().Name}.");
            _bySlot[member.Slot] = member;
        }
    }
}
