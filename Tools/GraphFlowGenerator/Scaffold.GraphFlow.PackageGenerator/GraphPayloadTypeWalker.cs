using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPayloadTypeWalker
    {
        /// <summary>Walks all types under <paramref name="assembly"/>'s global namespace (payload assembly for the runner), including when that assembly is only referenced (e.g. Editor compiling against Runtime).</summary>
        internal static ImmutableArray<INamedTypeSymbol> AllNamedTypesInAssembly(IAssemblySymbol assembly, CancellationToken ct)
        {
            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            WalkNamespace(assembly.GlobalNamespace, builder, ct);
            return builder.ToImmutable();
        }

        static void WalkNamespace(INamespaceSymbol ns, ImmutableArray<INamedTypeSymbol>.Builder into, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var m in ns.GetMembers())
            {
                if (m is INamespaceSymbol child)
                {
                    WalkNamespace(child, into, ct);
                }
                else if (m is INamedTypeSymbol named)
                {
                    AddTypeAndNested(named, into, ct);
                }
            }
        }

        static void AddTypeAndNested(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol>.Builder into, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            into.Add(type);
            foreach (var nested in type.GetTypeMembers())
            {
                AddTypeAndNested(nested, into, ct);
            }
        }
    }
}
