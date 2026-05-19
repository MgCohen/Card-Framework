using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Consolidated discovery walks that feed <see cref="GraphCatalogEmitter"/>. Each rule
    /// scopes to the package's runner asm — same scope used by every other pass — so unrelated
    /// asms in the project don't pollute the catalog.
    /// </summary>
    internal static class GraphCatalogDiscovery
    {
        /// <summary>Hardcoded Return primitives that always appear in every package's catalog.
        /// Tuple order: (LabelInEnum, FullyQualifiedTypeName, DefaultLiteral).</summary>
        static readonly (string Label, string TypeFq, string Default)[] ReturnPrimitives =
        {
            ("Int",    "int",    "0"),
            ("Bool",   "bool",   "false"),
            ("String", "string", "\"\""),
            ("Float",  "float",  "0f"),
        };

        internal static IReadOnlyList<GraphCatalogEmitter.EventDescriptor> DiscoverEvents(
            Compilation compilation,
            GraphPackageModel package,
            ImmutableArray<INamedTypeSymbol> allTypes,
            INamedTypeSymbol graphEventAttr,
            CancellationToken ct)
        {
            var result = new List<GraphCatalogEmitter.EventDescriptor>();
            foreach (var type in allTypes)
            {
                ct.ThrowIfCancellationRequested();
                if (!PayloadDiscovery.IsCandidateType(type)) continue;
                if (!PayloadDiscovery.HasGraphEventAttribute(type, graphEventAttr)) continue;

                var fields = PayloadDiscovery.InstanceFields(type);
                result.Add(new GraphCatalogEmitter.EventDescriptor(type, fields));
            }

            return result;
        }

        internal static IReadOnlyList<GraphCatalogEmitter.CommandDescriptor> DiscoverCommands(
            Compilation compilation,
            GraphPackageModel package,
            ImmutableArray<INamedTypeSymbol> allTypes,
            INamedTypeSymbol graphPortAttr,
            INamedTypeSymbol? graphPortIgnoreAttr,
            CancellationToken ct)
        {
            var result = new List<GraphCatalogEmitter.CommandDescriptor>();
            if (package.CommandBaseMetadataName == null) return result;

            var openCmdBase = compilation.GetTypeByMetadataName(package.CommandBaseMetadataName);
            if (openCmdBase == null) return result;

            foreach (var type in allTypes)
            {
                ct.ThrowIfCancellationRequested();
                if (!PayloadDiscovery.IsCandidateType(type)) continue;
                if (!PayloadDiscovery.TryGetCommandResultTypeFromBase(type, openCmdBase, out var resultType))
                    continue;
                if (resultType == null) continue;

                // Collect inputs from cmd / outputs from result, honoring the convention.
                var inputs = new List<IFieldSymbol>();
                foreach (var f in PayloadDiscovery.InstanceFields(type))
                {
                    if (PayloadDiscovery.IsPortField(f, package.Convention, graphPortAttr, graphPortIgnoreAttr))
                        inputs.Add(f);
                }

                var outputs = new List<IFieldSymbol>();
                foreach (var f in PayloadDiscovery.InstanceFields(resultType))
                {
                    if (PayloadDiscovery.IsPortField(f, package.Convention, graphPortAttr, graphPortIgnoreAttr))
                        outputs.Add(f);
                }

                var typeNs = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
                var dispatcherRuntimeFq = (string.IsNullOrEmpty(typeNs) ? "" : typeNs + ".") + type.Name + "DispatcherRuntime";

                result.Add(new GraphCatalogEmitter.CommandDescriptor(type, resultType, inputs, outputs, dispatcherRuntimeFq));
            }

            return result;
        }

        internal static IReadOnlyList<GraphCatalogEmitter.EntryDescriptor> DiscoverEntries(
            Compilation compilation,
            GraphPackageModel package,
            ImmutableArray<INamedTypeSymbol> allTypes,
            INamedTypeSymbol iEntry,
            INamedTypeSymbol graphPortAttr,
            INamedTypeSymbol? graphPortIgnoreAttr,
            CancellationToken ct)
        {
            var result = new List<GraphCatalogEmitter.EntryDescriptor>();
            foreach (var type in allTypes)
            {
                ct.ThrowIfCancellationRequested();
                if (!PayloadDiscovery.IsCandidateType(type)) continue;
                if (!PayloadDiscovery.IsGraphEntry(type, iEntry)) continue;

                var fields = new List<IFieldSymbol>();
                foreach (var f in PayloadDiscovery.InstanceFields(type))
                {
                    if (PayloadDiscovery.IsPortField(f, package.Convention, graphPortAttr, graphPortIgnoreAttr))
                        fields.Add(f);
                }

                var typeNs = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();
                var runtimeFq = (string.IsNullOrEmpty(typeNs) ? "" : typeNs + ".") + type.Name + "Runtime";

                result.Add(new GraphCatalogEmitter.EntryDescriptor(type, fields, runtimeFq));
            }

            return result;
        }

        internal static IReadOnlyList<GraphCatalogEmitter.ReturnDescriptor> DiscoverReturns(
            Compilation compilation,
            GraphPackageModel package,
            ImmutableArray<INamedTypeSymbol> allTypes,
            INamedTypeSymbol? graphReturnTypeAttr,
            CancellationToken ct)
        {
            var result = new List<GraphCatalogEmitter.ReturnDescriptor>();

            // Hardcoded primitives — always present in every package.
            foreach (var p in ReturnPrimitives)
            {
                result.Add(new GraphCatalogEmitter.ReturnDescriptor(p.Label, p.TypeFq, p.Default));
            }

            // [GraphReturnType]-tagged types in the package's runtime asm.
            if (graphReturnTypeAttr != null)
            {
                foreach (var type in allTypes)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!PayloadDiscovery.IsCandidateType(type)) continue;
                    if (!HasAttribute(type, graphReturnTypeAttr)) continue;

                    var typeFq = GraphCompilationNames.TrimGlobal(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    result.Add(new GraphCatalogEmitter.ReturnDescriptor(type.Name, typeFq, "default!"));
                }
            }

            return result;
        }

        static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attr)
        {
            foreach (var a in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attr))
                    return true;
            }
            return false;
        }

    }
}
