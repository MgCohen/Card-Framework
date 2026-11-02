using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.TypeRouting
{
    public static class TypeScan
    {
        // Static on purpose: loaded types do not change while the domain lives. A domain reload clears it.
        static readonly Dictionary<(Type, bool), Type[]> Cache = new();

        public static IReadOnlyList<Type> ConcreteTypesOf(Type baseType, bool includeTests = false)
        {
            var key = (baseType, includeTests);
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
            var reaching = NamesThatReach(
                baseType.Assembly.GetName().Name,
                loaded.Select(a => (a.GetName().Name, a.GetReferencedAssemblies().Select(r => r.Name))));

            var found = new List<Type>();
            foreach (var assembly in loaded)
            {
                if (!reaching.Contains(assembly.GetName().Name))
                    continue;
                if (!includeTests && References(assembly, "nunit.framework"))
                    continue;
                found.AddRange(ConcreteTypesIn(assembly, baseType));
            }

            // Assembly load order is not stable, so sort to give each process the same list.
            var result = found.OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
            Cache[key] = result;
            return result;
        }

        public static IReadOnlyList<Type> ConcreteTypesIn(Assembly assembly, Type baseType) =>
            LoadableTypes(assembly)
                .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition && baseType.IsAssignableFrom(t))
                .ToArray();

        // A member can derive through an intermediate assembly and not reference the home assembly directly.
        // So keep each assembly that reaches the home assembly through any chain of references.
        public static HashSet<string> NamesThatReach(string home, IEnumerable<(string Name, IEnumerable<string> References)> assemblies)
        {
            var graph = assemblies.Select(a => (a.Name, References: a.References.ToArray())).ToArray();
            var reaching = new HashSet<string> { home };
            bool added;
            do
            {
                added = false;
                foreach (var (name, references) in graph)
                    if (!reaching.Contains(name) && references.Any(reaching.Contains))
                        added = reaching.Add(name);
            }
            while (added);
            return reaching;
        }

        static bool References(Assembly assembly, string name) =>
            assembly.GetReferencedAssemblies().Any(r => r.Name == name);

        static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
