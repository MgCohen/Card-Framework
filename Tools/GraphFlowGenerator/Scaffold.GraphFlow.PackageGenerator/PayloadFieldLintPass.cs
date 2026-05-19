using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Walks every payload candidate in the package's runner asm and reports field-level
    /// diagnostics that the rest of the pipeline doesn't naturally surface:
    /// <list type="bullet">
    /// <item>EFG002 — [In] on a readonly field (input ports must be writable at hydration).</item>
    /// <item>EFG003 — [Out] on a settable field (in AttributedFields mode only).</item>
    /// <item>EFG004 — Port-classified field whose type isn't Unity-serializable.</item>
    /// <item>EFG006 — Field name collides with a reserved port label (FlowIn / FlowOut).</item>
    /// </list>
    /// <para>Runs once per package, only in the editor compilation pass, so each diagnostic
    /// surfaces exactly once across runtime + editor builds.</para>
    /// </summary>
    internal static class PayloadFieldLintPass
    {
        static readonly string[] ReservedPortNames = { "FlowIn", "FlowOut" };

        internal static void Run(SourceProductionContext spc, Compilation compilation, GraphPackageModel package, CancellationToken ct)
        {
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, package.RunnerFullyQualified);
            var payloadAsm = runner?.ContainingAssembly;
            if (payloadAsm == null) return;

            var inAttr      = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.InAttribute");
            var outAttr     = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.OutAttribute");
            var serAttr     = compilation.GetTypeByMetadataName("System.SerializableAttribute");
            var unityObject = compilation.GetTypeByMetadataName("UnityEngine.Object");
            var graphEventAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphEventAttribute");

            // Marker resolution for payload-kind detection.
            var markerNs = string.IsNullOrEmpty(package.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : package.GraphFrameworkNamespace;
            var iEntry  = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry");
            var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
            var iActionConstructed = (iAction != null && runner != null) ? iAction.Construct(runner) : null;
            var openCmdBase = string.IsNullOrEmpty(package.CommandBaseMetadataName)
                ? null
                : compilation.GetTypeByMetadataName(package.CommandBaseMetadataName);

            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAsm, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!PayloadDiscovery.IsCandidateType(type)) continue;
                if (!IsPayload(type, iEntry, iActionConstructed, openCmdBase, graphEventAttr)) continue;

                foreach (var field in PayloadDiscovery.InstanceFields(type))
                {
                    LintField(spc, package, field, inAttr, outAttr, serAttr, unityObject);
                }
            }
        }

        /// <summary>
        /// True iff <paramref name="type"/> is one of the four payload roles the framework
        /// recognizes — entry, action (for this package's runner), command, or [GraphEvent]. Other
        /// public classes (runtime nodes, result types, internal helpers) are out of scope for the
        /// field lint: their public fields aren't bake-captured user data.
        /// </summary>
        static bool IsPayload(INamedTypeSymbol type,
            INamedTypeSymbol? iEntry,
            INamedTypeSymbol? iActionConstructed,
            INamedTypeSymbol? openCmdBase,
            INamedTypeSymbol? graphEventAttr)
        {
            if (iEntry != null && PayloadDiscovery.IsGraphEntry(type, iEntry)) return true;
            if (iActionConstructed != null && PayloadDiscovery.Implements(type, iActionConstructed)) return true;
            if (openCmdBase != null && PayloadDiscovery.TryGetCommandResultTypeFromBase(type, openCmdBase, out _)) return true;
            if (graphEventAttr != null && PayloadDiscovery.HasGraphEventAttribute(type, graphEventAttr)) return true;
            return false;
        }

        static void LintField(SourceProductionContext spc, GraphPackageModel package, IFieldSymbol field,
            INamedTypeSymbol? inAttr, INamedTypeSymbol? outAttr,
            INamedTypeSymbol? serAttr, INamedTypeSymbol? unityObject)
        {
            var ownerName = field.ContainingType.Name;
            var loc = Diagnostics.LocationOf(field);

            // EFG006 — reserved port name clash. Cheap check first; fires regardless of mode/role.
            foreach (var reserved in ReservedPortNames)
            {
                if (string.Equals(field.Name, reserved, System.StringComparison.Ordinal))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.EFG006_ReservedPortName, loc, ownerName, field.Name));
                    break;
                }
            }

            var hasIn  = inAttr  != null && HasAttribute(field, inAttr);
            var hasOut = outAttr != null && HasAttribute(field, outAttr);

            // EFG002 — [In] on readonly. Doesn't depend on the package's convention; readonly
            // inputs are always nonsensical because the generator-emitted hydration writes the
            // field at bake time.
            if (hasIn && field.IsReadOnly)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EFG002_InOnReadonly, loc, ownerName, field.Name));
            }

            // EFG003 — [Out] on settable, scoped to AttributedFields mode. Other modes don't read
            // [Out] semantically, so flagging there would be noisy.
            if (hasOut && !field.IsReadOnly && package.Convention == PortConvention.AttributedFields)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EFG003_OutOnSettable, loc, ownerName, field.Name));
            }

            // EFG004 — non-serializable port field type. Only check fields that are likely to be
            // ports under SOME convention: AttributedFields requires [In]/[Out]; other modes treat
            // every public instance field as a port. We'll lint every public instance field that
            // could be a port, since that's the broadest superset.
            if (!IsLikelyUnitySerializable(field.Type, serAttr, unityObject))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.EFG004_FieldNotSerializable, loc, ownerName, field.Name,
                    field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        static bool HasAttribute(IFieldSymbol field, INamedTypeSymbol attrType)
        {
            foreach (var a in field.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Heuristic for Unity-style serialization. Conservative: covers primitives, string,
        /// enums, types tagged [Serializable], UnityEngine.Object derivatives, and List/array of
        /// any of the above. Anything else is flagged — the false-positive rate on legitimate
        /// custom shapes is acceptable because users can suppress per-line if needed.
        /// </summary>
        static bool IsLikelyUnitySerializable(ITypeSymbol type, INamedTypeSymbol? serAttr, INamedTypeSymbol? unityObject)
        {
            if (type is IArrayTypeSymbol arr)
            {
                return IsLikelyUnitySerializable(arr.ElementType, serAttr, unityObject);
            }

            if (type is INamedTypeSymbol named)
            {
                // List<T> — unwrap the element type. Other generics (Dictionary, Func, etc.) fall
                // through to the [Serializable] check, which they fail.
                if (named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        == "global::System.Collections.Generic.List<T>"
                    && named.TypeArguments.Length == 1)
                {
                    return IsLikelyUnitySerializable(named.TypeArguments[0], serAttr, unityObject);
                }

                // Primitives + string.
                switch (named.SpecialType)
                {
                    case SpecialType.System_Boolean:
                    case SpecialType.System_Char:
                    case SpecialType.System_SByte:
                    case SpecialType.System_Byte:
                    case SpecialType.System_Int16:
                    case SpecialType.System_UInt16:
                    case SpecialType.System_Int32:
                    case SpecialType.System_UInt32:
                    case SpecialType.System_Int64:
                    case SpecialType.System_UInt64:
                    case SpecialType.System_Single:
                    case SpecialType.System_Double:
                    case SpecialType.System_String:
                        return true;
                }

                // Enums.
                if (named.TypeKind == TypeKind.Enum) return true;

                // [Serializable] tagged.
                if (serAttr != null)
                {
                    foreach (var a in named.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, serAttr))
                            return true;
                    }
                }

                // UnityEngine.Object derivative.
                if (unityObject != null)
                {
                    for (var current = named.BaseType; current != null; current = current.BaseType)
                    {
                        if (SymbolEqualityComparer.Default.Equals(current, unityObject))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
