using System;
using System.Collections.Generic;
using System.Linq;

namespace Framework.Routing
{
    public sealed class TypeRouter<T> where T : class, IRouted
    {
        static readonly T[]    NoMembers = Array.Empty<T>();
        static readonly Type[] NoTypes   = Array.Empty<Type>();

        readonly Dictionary<Type, T[]>    _exact;
        readonly Dictionary<Type, T[]>    _derived;
        readonly Dictionary<Type, Type[]> _derivedTypes;

        public TypeRouter(IReadOnlyList<T> members, RouteMode mode)
        {
            // OrderBy is stable, so two members with the same order and type name keep their input order.
            var sorted = members
                .OrderBy(OrderOf)
                .ThenBy(m => m.GetType().FullName, StringComparer.Ordinal)
                .ToArray();

            var exact = new Dictionary<Type, List<T>>();
            var derived = new Dictionary<Type, List<T>>();
            var derivedTypes = new Dictionary<Type, List<Type>>();

            foreach (var member in sorted)
            {
                var type = member.RouteType;
                if (exact.TryGetValue(type, out var same))
                {
                    if (mode == RouteMode.Single)
                        throw new InvalidOperationException($"Duplicate {typeof(T).Name} for {NameOf(type)}.");
                }
                else
                {
                    exact[type] = same = new List<T>();
                }
                same.Add(member);

                foreach (var ancestor in AncestorsOf(type))
                {
                    Add(derived, ancestor, member);
                    if (!derivedTypes.TryGetValue(ancestor, out var types))
                        derivedTypes[ancestor] = types = new List<Type>();
                    if (!types.Contains(type))
                        types.Add(type);
                }
            }

            _exact = exact.ToDictionary(p => p.Key, p => p.Value.ToArray());
            _derived = derived.ToDictionary(p => p.Key, p => p.Value.ToArray());
            _derivedTypes = derivedTypes.ToDictionary(p => p.Key, p => p.Value.ToArray());
        }

        public IReadOnlyCollection<Type> RoutedTypes => _exact.Keys;

        public T Get(Type type)
        {
            if (!_exact.TryGetValue(type, out var members))
                throw new InvalidOperationException($"No {typeof(T).Name} registered for {NameOf(type)}.");
            return One(type, members);
        }

        public bool TryGet(Type type, out T member)
        {
            if (!_exact.TryGetValue(type, out var members))
            {
                member = null;
                return false;
            }
            member = One(type, members);
            return true;
        }

        public IReadOnlyList<T> GetAll(Type type) =>
            _exact.TryGetValue(type, out var members) ? members : NoMembers;

        public IReadOnlyList<T> GetAllDerivedFrom(Type type) =>
            _derived.TryGetValue(type, out var members) ? members : NoMembers;

        public IReadOnlyList<Type> RoutedTypesDerivedFrom(Type type) =>
            _derivedTypes.TryGetValue(type, out var types) ? types : NoTypes;

        static T One(Type type, T[] members)
        {
            if (members.Length > 1)
                throw new InvalidOperationException($"Many {typeof(T).Name} registered for {NameOf(type)}. Use GetAll.");
            return members[0];
        }

        // Type.Name shows "Gen`1" for a generic type. Show "Gen<Int32>" instead.
        static string NameOf(Type type)
        {
            var tick = type.Name.IndexOf('`');
            if (!type.IsGenericType || tick < 0)
                return type.Name;
            var name = type.Name.Substring(0, tick);
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(NameOf))}>";
        }

        static int OrderOf(T member) => member is IOrdered ordered ? ordered.Order : 0;

        static void Add(Dictionary<Type, List<T>> index, Type key, T member)
        {
            if (!index.TryGetValue(key, out var list))
                index[key] = list = new List<T>();
            list.Add(member);
        }

        static IEnumerable<Type> AncestorsOf(Type type)
        {
            var seen = new HashSet<Type>();
            var all = new List<Type>();
            for (var t = type; t != null; t = t.BaseType)
                all.Add(t);
            all.AddRange(type.GetInterfaces());

            foreach (var t in all)
            {
                if (seen.Add(t))
                    yield return t;
                if (t.IsGenericType && !t.IsGenericTypeDefinition && seen.Add(t.GetGenericTypeDefinition()))
                    yield return t.GetGenericTypeDefinition();
            }
        }
    }
}
