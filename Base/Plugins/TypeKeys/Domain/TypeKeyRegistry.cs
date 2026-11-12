using TypeKeys.Domain.Shared;

namespace TypeKeys.Domain;

public class TypeKeyRegistry<TKey> : ICodeMemberScan where TKey : TypeKey
{
    private readonly Dictionary<int, TKey> bySlot = new Dictionary<int, TKey>();
    private readonly Dictionary<Type, TKey> byType = new Dictionary<Type, TKey>();
    private IReadOnlyCollection<TKey>? members;
    private bool frozen;

    public IReadOnlyCollection<TKey> Members =>
        members ??= bySlot.OrderBy(p => p.Key).Select(p => p.Value).ToList().AsReadOnly();

    public void RegisterCodeMembers(bool includeTests)
    {
        foreach (Type type in TypeScan.ConcreteTypesOf(typeof(TKey), includeTests))
        {
            if (IsCodeMember(type))
            {
                Register((TKey)Activator.CreateInstance(type)!);
            }
        }
    }

    public void Register<T>() where T : TKey, new()
    {
        Register(new T());
    }

    public void Register(TKey member)
    {
        Add(member);
        if (IsCodeMember(member.GetType()))
        {
            byType.TryAdd(member.GetType(), member);
        }
    }

    public T Define<T>(int slot, Func<int, T> make) where T : TKey
    {
        T member = make(slot);
        Add(member);
        return member;
    }

    public void Freeze()
    {
        frozen = true;
    }

    public int SlotOf(TKey key)
    {
        return key.Slot;
    }

    public TKey BySlot(int slot)
    {
        return bySlot.TryGetValue(slot, out TKey? member)
            ? member
            : throw new KeyNotFoundException($"{GetType().Name}: no member has slot {slot}.");
    }

    public bool TryBySlot(int slot, out TKey key)
    {
        return bySlot.TryGetValue(slot, out key!);
    }

    public T Of<T>() where T : TKey
    {
        return byType.TryGetValue(typeof(T), out TKey? member)
            ? (T)member
            : throw new KeyNotFoundException($"{GetType().Name}: {typeof(T).Name} is not registered.");
    }

    private static bool IsCodeMember(Type type)
    {
        return type.IsSealed && type.GetConstructor(Type.EmptyTypes) != null;
    }

    private void Add(TKey member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (frozen)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} is frozen. Register {member.GetType().Name} before Freeze.");
        }

        if (bySlot.TryGetValue(member.Slot, out TKey? old))
        {
            if (old.Equals(member))
            {
                return;
            }

            throw new InvalidOperationException(
                $"{GetType().Name}: slot {member.Slot} has {old.GetType().Name} and {member.GetType().Name}.");
        }

        bySlot.Add(member.Slot, member);
        members = null;
    }
}
