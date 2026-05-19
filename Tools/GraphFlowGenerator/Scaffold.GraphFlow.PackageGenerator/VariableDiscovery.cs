using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal readonly struct VariableTypeInfo
    {
        internal VariableTypeInfo(string label, string valueTypeFq, string valueTypeSimple, string subclassNamespace)
        {
            Label = label;
            ValueTypeFq = valueTypeFq;
            ValueTypeSimple = valueTypeSimple;
            SubclassNamespace = subclassNamespace;
        }

        /// <summary>Derived from the subclass name by stripping the "Blackboard" prefix (e.g. "Int", "Float", "Vector3").</summary>
        internal string Label { get; }
        /// <summary>Fully qualified value type (e.g. "int", "float", "UnityEngine.Vector3").</summary>
        internal string ValueTypeFq { get; }
        /// <summary>C# keyword or short form (e.g. "int", "float", "UnityEngine.Vector3").</summary>
        internal string ValueTypeSimple { get; }
        /// <summary>Namespace of the BlackboardVariable subclass.</summary>
        internal string SubclassNamespace { get; }
    }

    internal static class VariableDiscovery
    {
        const string BlackboardVariableGenericFqn = "Scaffold.GraphFlow.BlackboardVariable`1";

        internal static ImmutableArray<VariableTypeInfo> Discover(Compilation compilation, CancellationToken ct)
        {
            var baseType = compilation.GetTypeByMetadataName(BlackboardVariableGenericFqn);
            if (baseType == null)
                return ImmutableArray<VariableTypeInfo>.Empty;

            var builder = ImmutableArray.CreateBuilder<VariableTypeInfo>();
            foreach (var type in AllSourceTypes(compilation, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (type.TypeKind != TypeKind.Class || type.IsAbstract) continue;
                if (type.DeclaredAccessibility != Accessibility.Public) continue;

                var closedBase = FindClosedBlackboardBase(type, baseType);
                if (closedBase == null) continue;

                var valueType = closedBase.TypeArguments[0];
                var label = DeriveLabel(type.Name);
                if (string.IsNullOrEmpty(label)) continue;

                var ns = type.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : type.ContainingNamespace.ToDisplayString();

                builder.Add(new VariableTypeInfo(
                    label,
                    TypeFmt.Simple(valueType),
                    TypeFmt.Simple(valueType),
                    ns));
            }

            builder.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return builder.ToImmutable();
        }

        internal static ImmutableArray<VariableTypeInfo> DiscoverFromAssembly(
            Compilation compilation, IAssemblySymbol assembly, CancellationToken ct)
        {
            var allTypes = GraphPayloadTypeWalker.AllNamedTypesInAssembly(assembly, ct);
            return DiscoverFromTypes(compilation, allTypes, ct);
        }

        internal static ImmutableArray<VariableTypeInfo> DiscoverFromTypes(
            Compilation compilation, ImmutableArray<INamedTypeSymbol> types, CancellationToken ct)
        {
            var baseType = compilation.GetTypeByMetadataName(BlackboardVariableGenericFqn);
            if (baseType == null)
                return ImmutableArray<VariableTypeInfo>.Empty;

            var builder = ImmutableArray.CreateBuilder<VariableTypeInfo>();
            foreach (var type in types)
            {
                ct.ThrowIfCancellationRequested();
                if (type.TypeKind != TypeKind.Class || type.IsAbstract) continue;
                if (type.DeclaredAccessibility != Accessibility.Public) continue;

                var closedBase = FindClosedBlackboardBase(type, baseType);
                if (closedBase == null) continue;

                var valueType = closedBase.TypeArguments[0];
                var label = DeriveLabel(type.Name);
                if (string.IsNullOrEmpty(label)) continue;

                var ns = type.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : type.ContainingNamespace.ToDisplayString();

                builder.Add(new VariableTypeInfo(
                    label,
                    TypeFmt.Simple(valueType),
                    TypeFmt.Simple(valueType),
                    ns));
            }

            builder.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return builder.ToImmutable();
        }

        static INamedTypeSymbol? FindClosedBlackboardBase(INamedTypeSymbol type, INamedTypeSymbol openBase)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, openBase))
                {
                    return current;
                }
            }
            return null;
        }

        static string DeriveLabel(string className)
        {
            const string prefix = "Blackboard";
            if (className.StartsWith(prefix, System.StringComparison.Ordinal) && className.Length > prefix.Length)
                return className.Substring(prefix.Length);
            return className;
        }

        static System.Collections.Generic.IEnumerable<INamedTypeSymbol> AllSourceTypes(
            Compilation compilation, CancellationToken ct)
        {
            return WalkNamespace(compilation.Assembly.GlobalNamespace, ct);
        }

        static System.Collections.Generic.IEnumerable<INamedTypeSymbol> WalkNamespace(
            INamespaceSymbol ns, CancellationToken ct)
        {
            foreach (var member in ns.GetTypeMembers())
            {
                ct.ThrowIfCancellationRequested();
                yield return member;
                foreach (var nested in member.GetTypeMembers())
                    yield return nested;
            }
            foreach (var child in ns.GetNamespaceMembers())
            {
                foreach (var type in WalkNamespace(child, ct))
                    yield return type;
            }
        }
    }
}
