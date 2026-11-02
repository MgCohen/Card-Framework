using System;
using System.Collections.Generic;

namespace Framework.Kinds
{
    public abstract partial record TypeKey
    {
        public class Registry<TKey> where TKey : TypeKey
        {
            readonly Dictionary<int, TKey> _bySlot = new();
            readonly Dictionary<Type, TKey> _byType = new();
            bool _frozen;

            public IReadOnlyCollection<TKey> Members => _bySlot.Values;

            public void Register<T>() where T : TKey, new() => Register(new T());

            public void Register(TKey member)
            {
                Add(member);
                _byType.TryAdd(member.GetType(), member);
            }

            // A data member has no type of its own, so Of<T> does not find it.
            public T Define<T>(int slot, Func<int, T> make) where T : TKey
            {
                var member = make(slot);
                Add(member);
                return member;
            }

            public void Freeze() => _frozen = true;

            public int SlotOf(TKey key) => key._slot;

            public TKey BySlot(int slot) =>
                _bySlot.TryGetValue(slot, out var member)
                    ? member
                    : throw new KeyNotFoundException($"{GetType().Name}: no member has slot {slot}.");

            public bool TryBySlot(int slot, out TKey key) => _bySlot.TryGetValue(slot, out key);

            public T Of<T>() where T : TKey =>
                _byType.TryGetValue(typeof(T), out var member)
                    ? (T)member
                    : throw new KeyNotFoundException($"{GetType().Name}: {typeof(T).Name} is not registered.");

            void Add(TKey member)
            {
                if (member == null)
                    throw new ArgumentNullException(nameof(member));
                if (_frozen)
                    throw new InvalidOperationException($"{GetType().Name} is frozen. Register {member.GetType().Name} before Freeze.");
                if (_bySlot.TryGetValue(member._slot, out var old))
                {
                    if (old.Equals(member))
                        return;
                    throw new InvalidOperationException(
                        $"{GetType().Name}: slot {member._slot} has {old.GetType().Name} and {member.GetType().Name}.");
                }
                _bySlot.Add(member._slot, member);
            }
        }
    }
}
