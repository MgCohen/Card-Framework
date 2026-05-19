using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Whole-compilation lint that flags every class derived from
    /// <c>Scaffold.GraphFlow.RuntimeNode</c> or <c>Unity.GraphToolkit.Editor.Node</c> without an
    /// explicit <c>[System.Serializable]</c> attribute. Unity walks the polymorphic inheritance
    /// chain at <c>[SerializeReference]</c> deserialization time and emits a runtime warning per
    /// unmarked link — this lint catches those at edit time so the build is the source of truth.
    ///
    /// <para>Runs once per compilation pass (runtime asm and editor asm both walked separately;
    /// each only sees its own source types so there's no double-reporting).</para>
    /// </summary>
    internal static class SerializableLintPass
    {
        internal static void Run(SourceProductionContext spc, Compilation compilation, CancellationToken ct)
        {
            var serializableAttr = compilation.GetTypeByMetadataName("System.SerializableAttribute");
            if (serializableAttr == null) return;

            var runtimeNodeBase  = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.RuntimeNode");
            var graphToolkitNode = compilation.GetTypeByMetadataName("Unity.GraphToolkit.Editor.Node");
            if (runtimeNodeBase == null && graphToolkitNode == null) return;

            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(compilation.Assembly, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (type.TypeKind != TypeKind.Class) continue;

                // Skip the bases themselves — checking what they inherit from isn't the point.
                if (SymbolEqualityComparer.Default.Equals(type, runtimeNodeBase)) continue;
                if (SymbolEqualityComparer.Default.Equals(type, graphToolkitNode)) continue;

                var baseLabel = ClassifyBase(type, runtimeNodeBase, graphToolkitNode);
                if (baseLabel == null) continue;

                if (HasSerializable(type, serializableAttr)) continue;

                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EFG009_MissingSerializable,
                    Diagnostics.LocationOf(type),
                    type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    baseLabel));
            }
        }

        /// <summary>Walks the base chain. Returns "Scaffold.GraphFlow.RuntimeNode" or
        /// "Unity.GraphToolkit.Editor.Node" if either is reached, else null.</summary>
        static string? ClassifyBase(INamedTypeSymbol type, INamedTypeSymbol? runtimeBase, INamedTypeSymbol? nodeBase)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                // RuntimeNode<T> closes to a constructed generic; compare against the original
                // definition so RuntimeNode and RuntimeNode<TRunner> both match.
                var def = current.OriginalDefinition;
                if (runtimeBase != null && SymbolEqualityComparer.Default.Equals(def, runtimeBase))
                    return "Scaffold.GraphFlow.RuntimeNode";
                if (nodeBase != null && SymbolEqualityComparer.Default.Equals(def, nodeBase))
                    return "Unity.GraphToolkit.Editor.Node";
            }

            return null;
        }

        static bool HasSerializable(INamedTypeSymbol type, INamedTypeSymbol serializableAttr)
        {
            foreach (var a in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, serializableAttr))
                    return true;
            }
            return false;
        }
    }
}
