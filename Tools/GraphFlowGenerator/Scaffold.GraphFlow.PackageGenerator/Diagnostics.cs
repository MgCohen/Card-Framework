using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class Diagnostics
    {
        const string Category = "GraphFlow";

        // EFG007 — Action payload has no execution path.
        internal static readonly DiagnosticDescriptor EFG007_NoExecutionPath = new(
            id: "EFG007",
            title: "Action payload has no execution path",
            messageFormat: "Payload '{0}' implements IGraphAction<{1}> but has no execution path. Either implement IExecutable<{1}>, declare DispatcherBase + CommandBase on [GraphPackage] and extend the CommandBase, or hand-author a runtime node.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG008 — Payload satisfies bindings for two different [GraphPackage] declarations.
        internal static readonly DiagnosticDescriptor EFG008_MultiPackageBinding = new(
            id: "EFG008",
            title: "Payload bound to multiple GraphPackages",
            messageFormat: "Payload '{0}' satisfies bindings for both runners '{1}' and '{2}'. Each payload must belong to exactly one [GraphPackage].",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG009 — Type derived from RuntimeNode / GraphToolkit's Node missing [Serializable].
        // Every link in the inheritance chain of a [SerializeReference] target needs
        // [System.Serializable] explicitly (the attribute is not inherited). Unity logs a runtime
        // warning per missing link; this diagnostic catches them at edit time so they don't slip in.
        internal static readonly DiagnosticDescriptor EFG009_MissingSerializable = new(
            id: "EFG009",
            title: "Polymorphic graph type missing [Serializable]",
            messageFormat: "Type '{0}' derives from {1} and is serialized by Unity's [SerializeReference], but is missing the [Serializable] attribute. Add [System.Serializable] (or [MakeSerializable]) on the class declaration to suppress Unity's runtime warning.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG002 — [In] attribute on a readonly field. The In direction implies the runtime writes
        // the value during hydration (port read), but readonly fields can only be assigned in a
        // constructor — the generator can't populate them. Either drop the readonly modifier or
        // remove [In].
        internal static readonly DiagnosticDescriptor EFG002_InOnReadonly = new(
            id: "EFG002",
            title: "[In] on readonly field",
            messageFormat: "Field '{0}.{1}' is marked [In] but is declared readonly — the runtime can't populate it from the input port. Remove either the readonly modifier or the [In] attribute.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG003 — [Out] attribute on a settable field in AttributedFields mode. [Out] semantically
        // means "exposed as a data output read by other nodes"; settable fields suggest the
        // author intended an input. Fires only in AttributedFields mode (in other modes [Out] has
        // different / no meaning).
        internal static readonly DiagnosticDescriptor EFG003_OutOnSettable = new(
            id: "EFG003",
            title: "[Out] on settable field",
            messageFormat: "Field '{0}.{1}' is marked [Out] but is settable — [Out] is for read-only outputs that downstream nodes consume. Mark the field readonly, or use [In] if it's meant to be an input.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG004 — Port-classified field whose type is not Unity-serializable. Unity's
        // [SerializeReference] (and [SerializeField]) only round-trips a known shape: primitives,
        // string, enums, [Serializable] types, and UnityEngine.Object derivatives (plus List/array
        // of any of those). Fields outside this set won't survive bake → reload.
        internal static readonly DiagnosticDescriptor EFG004_FieldNotSerializable = new(
            id: "EFG004",
            title: "Port field type not Unity-serializable",
            messageFormat: "Field '{0}.{1}' has type '{2}' which is not Unity-serializable (not a primitive, string, enum, [Serializable] type, or UnityEngine.Object derivative). The bake won't preserve its value across the asset round-trip.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // EFG006 — Field name conflicts with a generated port label. The generator emits implicit
        // FlowIn / FlowOut ports for entry / action / dispatcher payloads; a payload field with
        // the same name would create a duplicate port in the editor mirror.
        internal static readonly DiagnosticDescriptor EFG006_ReservedPortName = new(
            id: "EFG006",
            title: "Field name conflicts with reserved port",
            messageFormat: "Field '{0}.{1}' uses the reserved port name '{1}' (collides with the generator-emitted flow port). Rename the field — for example, prefix it with the payload concept (e.g., 'Card{1}').",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        internal static Location LocationOf(ISymbol symbol)
        {
            foreach (var loc in symbol.Locations)
            {
                if (loc.IsInSource)
                {
                    return loc;
                }
            }

            return Location.None;
        }
    }
}
