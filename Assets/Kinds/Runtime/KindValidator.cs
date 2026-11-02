using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Routing;

namespace Framework.Kinds
{
    // Returns the shape issues of each kind root. Throws nothing. No roots means all roots.
    public static class KindValidator
    {
        public static IReadOnlyList<string> Validate(params Type[] roots) => Validate(false, roots);

        public static IReadOnlyList<string> Validate(bool includeTests, params Type[] roots)
        {
            if (roots == null || roots.Length == 0)
                roots = AllRoots(includeTests);

            var issues = new List<string>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    issues.Add("A root is null.");
                    continue;
                }
                if (!typeof(TypeKey).IsAssignableFrom(root) || root == typeof(TypeKey))
                {
                    issues.Add($"{root.Name} is not a kind root: it does not derive from TypeKey.");
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
                    issues.Add($"KIND-01 {root.Name}: {Unwrap(e).Message}");
                }
            }
            return issues;
        }

        // Warning: this scan does not find a root with no concrete member. Name such a root in roots.
        static Type[] AllRoots(bool includeTests) =>
            TypeScan.ConcreteTypesOf(typeof(TypeKey), includeTests)
                .Select(RootOf)
                .Where(t => t.IsAbstract)
                .Distinct()
                .ToArray();

        static Type RootOf(Type type)
        {
            while (type.BaseType != typeof(TypeKey))
                type = type.BaseType;
            return type;
        }

        // KIND-01: each registry fills with no slot clash, and each member constructor runs.
        static void CheckSlots(Type root, bool includeTests, List<string> issues)
        {
            try
            {
                var registry = (ICodeMemberScan)Activator.CreateInstance(typeof(TypeKey.Registry<>).MakeGenericType(root));
                registry.RegisterCodeMembers(includeTests);
            }
            catch (Exception e)
            {
                issues.Add($"KIND-01 {root.Name}: {Unwrap(e).Message}");
            }
        }

        // KIND-02: a member type has no public constructor with parameters.
        static void CheckConstructors(Type root, bool includeTests, List<string> issues)
        {
            foreach (var type in TypeScan.ConcreteTypesOf(root, includeTests))
                if (type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any(c => c.GetParameters().Length > 0))
                    issues.Add($"KIND-02 {root.Name}: {type.Name} has a public constructor with parameters. Make it internal or private.");
        }

        // KIND-03: a root record is not positional. A positional record declares Deconstruct.
        static void CheckPositional(Type root, List<string> issues)
        {
            if (root.GetMethod("Deconstruct", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) != null)
                issues.Add($"KIND-03 {root.Name}: a kind root is not positional. Use a protected constructor.");
        }

        static Exception Unwrap(Exception e) => e is TargetInvocationException { InnerException: { } inner } ? inner : e;
    }
}
