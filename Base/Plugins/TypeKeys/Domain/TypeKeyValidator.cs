using System.Reflection;
using TypeKeys.Domain.Shared;

namespace TypeKeys.Domain;

public static class TypeKeyValidator
{
    public static IReadOnlyList<string> Validate(params Type?[]? roots)
    {
        return Validate(false, roots);
    }

    public static IReadOnlyList<string> Validate(bool includeTests, params Type?[]? roots)
    {
        if (roots == null || roots.Length == 0)
        {
            roots = AllRoots(includeTests);
        }

        List<string> issues = new List<string>();
        foreach (Type? root in roots)
        {
            if (root == null)
            {
                issues.Add("A root is null.");
                continue;
            }

            if (!typeof(TypeKey).IsAssignableFrom(root) || root == typeof(TypeKey))
            {
                issues.Add($"{root.Name} is not a key root: it does not derive from TypeKey.");
                continue;
            }

            try
            {
                CheckSlots(root, includeTests, issues);
                CheckConstructors(root, includeTests, issues);
                CheckPositional(root, issues);
            }
            catch (Exception e)
            {
                issues.Add($"TYPEKEY-01 {root.Name}: {Unwrap(e).Message}");
            }
        }

        return issues;
    }

    internal static Type[] AllRoots(bool includeTests)
    {
        return TypeScan.ConcreteTypesOf(typeof(TypeKey), includeTests)
            .Select(RootOf)
            .Where(t => t.IsAbstract)
            .Distinct()
            .ToArray();
    }

    private static Type RootOf(Type type)
    {
        while (type.BaseType != typeof(TypeKey))
        {
            type = type.BaseType!;
        }

        return type;
    }

    private static void CheckSlots(Type root, bool includeTests, List<string> issues)
    {
        try
        {
            Type registryType = typeof(TypeKeyRegistry<>).MakeGenericType(root);
            ICodeMemberScan registry = (ICodeMemberScan)Activator.CreateInstance(registryType)!;
            registry.RegisterCodeMembers(includeTests);
        }
        catch (Exception e)
        {
            issues.Add($"TYPEKEY-01 {root.Name}: {Unwrap(e).Message}");
        }
    }

    private static void CheckConstructors(Type root, bool includeTests, List<string> issues)
    {
        foreach (Type type in TypeScan.ConcreteTypesOf(root, includeTests))
        {
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Any(c => c.GetParameters().Length > 0))
            {
                issues.Add($"TYPEKEY-02 {root.Name}: {type.Name} has a public constructor with parameters. "
                    + "Make it internal or private.");
            }
        }
    }

    private static void CheckPositional(Type root, List<string> issues)
    {
        BindingFlags declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        if (root.GetMethod("Deconstruct", declared) != null)
        {
            issues.Add($"TYPEKEY-03 {root.Name}: a key root is not positional. Use a protected constructor.");
        }
    }

    private static Exception Unwrap(Exception e)
    {
        return e is TargetInvocationException { InnerException: { } inner } ? inner : e;
    }
}
