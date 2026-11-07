namespace Pipeline.Slices.Run;

internal sealed class TypeRouter<T>
    where T : class, IRouted
{
    private static readonly T[] noMembers = Array.Empty<T>();
    private static readonly Type[] noTypes = Array.Empty<Type>();

    private readonly Dictionary<Type, T[]> exact;
    private readonly Dictionary<Type, T[]> derived;
    private readonly Dictionary<Type, Type[]> derivedTypes;

    public TypeRouter(IReadOnlyList<T> members, RouteMode mode)
    {
        T[] sorted = members
            .OrderBy(OrderOf)
            .ThenBy(m => m.GetType().FullName, StringComparer.Ordinal)
            .ToArray();

        Dictionary<Type, List<T>> exactLists = new Dictionary<Type, List<T>>();
        Dictionary<Type, List<T>> derivedLists = new Dictionary<Type, List<T>>();
        Dictionary<Type, List<Type>> derivedTypeLists = new Dictionary<Type, List<Type>>();

        foreach (T member in sorted)
        {
            Type type = member.RouteType;
            if (exactLists.TryGetValue(type, out List<T>? same))
            {
                if (mode == RouteMode.Single)
                {
                    throw new InvalidOperationException($"Duplicate {typeof(T).Name} for {NameOf(type)}.");
                }
            }
            else
            {
                same = new List<T>();
                exactLists[type] = same;
            }

            same.Add(member);
            AddAncestors(derivedLists, derivedTypeLists, type, member);
        }

        exact = exactLists.ToDictionary(p => p.Key, p => p.Value.ToArray());
        derived = derivedLists.ToDictionary(p => p.Key, p => p.Value.ToArray());
        derivedTypes = derivedTypeLists.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    public IReadOnlyCollection<Type> RoutedTypes => exact.Keys;

    public T Get(Type type)
    {
        if (!exact.TryGetValue(type, out T[]? members))
        {
            throw new InvalidOperationException($"No {typeof(T).Name} registered for {NameOf(type)}.");
        }

        return One(type, members);
    }

    public bool TryGet(Type type, out T? member)
    {
        if (!exact.TryGetValue(type, out T[]? members))
        {
            member = null;
            return false;
        }

        member = One(type, members);
        return true;
    }

    public IReadOnlyList<T> GetAll(Type type)
    {
        return exact.TryGetValue(type, out T[]? members) ? members : noMembers;
    }

    public IReadOnlyList<T> GetAllDerivedFrom(Type type)
    {
        return derived.TryGetValue(type, out T[]? members) ? members : noMembers;
    }

    public IReadOnlyList<Type> RoutedTypesDerivedFrom(Type type)
    {
        return derivedTypes.TryGetValue(type, out Type[]? types) ? types : noTypes;
    }

    private static T One(Type type, T[] members)
    {
        if (members.Length > 1)
        {
            throw new InvalidOperationException($"Many {typeof(T).Name} registered for {NameOf(type)}. Use GetAll.");
        }

        return members[0];
    }

    private static string NameOf(Type type)
    {
        int tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        if (!type.IsGenericType || tick < 0)
        {
            return type.Name;
        }

        string name = type.Name.Substring(0, tick);
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(NameOf))}>";
    }

    private static int OrderOf(T member)
    {
        return member is IOrdered ordered ? ordered.Order : 0;
    }

    private static void AddAncestors(
        Dictionary<Type, List<T>> derivedLists,
        Dictionary<Type, List<Type>> derivedTypeLists,
        Type type,
        T member)
    {
        foreach (Type ancestor in AncestorsOf(type))
        {
            Add(derivedLists, ancestor, member);
            if (!derivedTypeLists.TryGetValue(ancestor, out List<Type>? types))
            {
                types = new List<Type>();
                derivedTypeLists[ancestor] = types;
            }

            if (!types.Contains(type))
            {
                types.Add(type);
            }
        }
    }

    private static void Add(Dictionary<Type, List<T>> index, Type key, T member)
    {
        if (!index.TryGetValue(key, out List<T>? list))
        {
            list = new List<T>();
            index[key] = list;
        }

        list.Add(member);
    }

    private static IEnumerable<Type> AncestorsOf(Type type)
    {
        HashSet<Type> seen = new HashSet<Type>();
        List<Type> all = new List<Type>();
        for (Type? t = type; t != null; t = t.BaseType)
        {
            all.Add(t);
        }

        all.AddRange(type.GetInterfaces());

        foreach (Type t in all)
        {
            if (seen.Add(t))
            {
                yield return t;
            }

            if (t.IsGenericType && !t.IsGenericTypeDefinition && seen.Add(t.GetGenericTypeDefinition()))
            {
                yield return t.GetGenericTypeDefinition();
            }
        }
    }
}
