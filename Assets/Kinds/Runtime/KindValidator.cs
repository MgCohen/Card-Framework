using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Routing;

namespace Framework.Kinds
{
    // Returns the shape issues of each kind root. Throws nothing.
    public static class KindValidator
    {
        public static IReadOnlyList<string> Validate(params Type[] roots) => Validate(true, roots);

        public static IReadOnlyList<string> Validate(bool includeTests, params Type[] roots)
        {
            var issues = new List<string>();
            foreach (var root in roots)
            {
                if (!typeof(TypeKey).IsAssignableFrom(root) || root == typeof(TypeKey))
                {
                    issues.Add($"{root.Name} is not a kind root: it does not derive from TypeKey.");
                    continue;
                }
                CheckSlots(root, includeTests, issues);
                CheckConstructors(root, includeTests, issues);
                CheckPositional(root, issues);
            }
            return issues;
        }

        // KIND-01: each registry fills with no slot clash.
        static void CheckSlots(Type root, bool includeTests, List<string> issues)
        {
            var registry = (ICodeMemberScan)Activator.CreateInstance(typeof(TypeKey.Registry<>).MakeGenericType(root));
            try
            {
                registry.RegisterCodeMembers(includeTests);
            }
            catch (InvalidOperationException e)
            {
                issues.Add($"KIND-01 {root.Name}: {e.Message}");
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
    }
}
