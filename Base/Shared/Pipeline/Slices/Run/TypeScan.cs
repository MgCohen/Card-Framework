using System.Collections.Concurrent;
using System.Reflection;

namespace Pipeline.Slices.Run;

internal static class TypeScan
{
    private static readonly ConcurrentDictionary<(Type, bool), Type[]> cache =
        new ConcurrentDictionary<(Type, bool), Type[]>();

    public static IReadOnlyList<Type> ConcreteTypesOf(Type baseType, bool includeTests = false)
    {
        (Type, bool) key = (baseType, includeTests);
        if (cache.TryGetValue(key, out Type[]? cached))
        {
            return cached;
        }

        Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToArray();
        HashSet<string> reaching = NamesThatReach(
            baseType.Assembly.GetName().Name!,
            loaded.Select(a => (a.GetName().Name!, a.GetReferencedAssemblies().Select(r => r.Name!))));

        List<Type> found = new List<Type>();
        foreach (Assembly assembly in loaded)
        {
            if (!reaching.Contains(assembly.GetName().Name!))
            {
                continue;
            }

            if (!includeTests && References(assembly, "nunit.framework"))
            {
                continue;
            }

            found.AddRange(ConcreteTypesIn(assembly, baseType));
        }

        Type[] result = found.OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        cache[key] = result;
        return result;
    }

    public static IReadOnlyList<Type> ConcreteTypesIn(Assembly assembly, Type baseType)
    {
        return LoadableTypes(assembly)
            .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition && baseType.IsAssignableFrom(t))
            .ToArray();
    }

    public static HashSet<string> NamesThatReach(
        string home,
        IEnumerable<(string Name, IEnumerable<string> References)> assemblies)
    {
        (string Name, string[] References)[] graph = assemblies.Select(a => (a.Name, a.References.ToArray())).ToArray();
        HashSet<string> reaching = new HashSet<string> { home };
        bool added;
        do
        {
            added = false;
            foreach ((string name, string[] references) in graph)
            {
                if (!reaching.Contains(name) && references.Any(reaching.Contains))
                {
                    added = reaching.Add(name);
                }
            }
        }
        while (added);
        return reaching;
    }

    private static bool References(Assembly assembly, string name)
    {
        return assembly.GetReferencedAssemblies().Any(r => r.Name == name);
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null).Select(t => t!);
        }
    }
}
