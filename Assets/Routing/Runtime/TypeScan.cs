using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.Routing
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

            var home = baseType.Assembly;
            var found = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;
                if (assembly != home && !References(assembly, home.GetName().Name))
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
