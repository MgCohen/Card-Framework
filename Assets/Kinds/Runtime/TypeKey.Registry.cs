using System;
using System.Collections.Generic;
using System.Linq;
using Framework.Routing;

namespace Framework.Kinds
{
    public abstract partial record TypeKey
    {
        public class Registry<TKey> : ICodeMemberScan where TKey : TypeKey
        {
            private readonly Dictionary<int, TKey> _bySlot = new();
            private readonly Dictionary<Type, TKey> _byType = new();
            private IReadOnlyCollection<TKey> _members;
            private bool _frozen;

            // Sorted by slot, so the order does not depend on the scan order.
            public IReadOnlyCollection<TKey> Members => _members ??= _bySlot.OrderBy(p => p.Key).Select(p => p.Value).ToList().AsReadOnly();

            public void RegisterCodeMembers(bool includeTests)
            {
                foreach (var type in TypeScan.ConcreteTypesOf(typeof(TKey), includeTests))
                    if (IsCodeMember(type))
                        Register((TKey)Activator.CreateInstance(type));
            }

            public void Register<T>() where T : TKey, new() => Register(new T());

            public void Register(TKey member)
            {
                Add(member);
                if (IsCodeMember(member.GetType()))
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

            private void Add(TKey member)
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
                _members = null;
            }

            private static bool IsCodeMember(Type type) => type.IsSealed && type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
