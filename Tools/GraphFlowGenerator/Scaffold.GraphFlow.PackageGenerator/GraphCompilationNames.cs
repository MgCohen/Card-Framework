using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphCompilationNames
    {
        internal static string EditorPackageRoot(Compilation compilation)
        {
            var name = compilation.Assembly.Name ?? "";
            const string suffix = ".Editor";
            if (name.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }

            return name;
        }

        internal static string EditorGraphToolkitNamespace(Compilation compilation) => EditorPackageRoot(compilation) + ".Editor.GToolkit";

        internal static INamedTypeSymbol? TypeFromFullyQualified(Compilation compilation, string fullyQualified)
        {
            var trimmed = TrimGlobal(fullyQualified);
            return compilation.GetTypeByMetadataName(trimmed);
        }

        internal static string TrimGlobal(string fq) =>
            fq.StartsWith("global::", System.StringComparison.Ordinal) ? fq.Substring(8) : fq;

        internal static bool IsEditorAssembly(Compilation compilation)
        {
            var name = compilation.Assembly.Name;
            return name != null && name.EndsWith(".Editor", System.StringComparison.Ordinal);
        }
    }
}
