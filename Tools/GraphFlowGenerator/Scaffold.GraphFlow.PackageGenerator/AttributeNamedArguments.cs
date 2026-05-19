using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    static class AttributeNamedArguments
    {
        internal static INamedTypeSymbol? TryGetNamedType(AttributeData attr, string name)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != name)
                {
                    continue;
                }

                if (kvp.Value.Value is INamedTypeSymbol t)
                {
                    return t;
                }
            }

            return null;
        }

        internal static int? TryGetNamedInt(AttributeData attr, string name)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != name)
                {
                    continue;
                }

                if (kvp.Value.Value is int i)
                {
                    return i;
                }
            }

            return null;
        }

        internal static void ReadPackageStrings(AttributeData attr, out string extension, out string assetMenu, out string registryNs)
        {
            extension = TryGetNamedString(attr, "Extension") ?? "";
            assetMenu = TryGetNamedString(attr, "AssetMenu") ?? "";
            registryNs = TryGetNamedString(attr, "RegistryNamespace") ?? "";
        }

        static string? TryGetNamedString(AttributeData attr, string name)
        {
            foreach (var kvp in attr.NamedArguments)
            {
                if (kvp.Key != name)
                {
                    continue;
                }

                if (kvp.Value.Value is string s)
                {
                    return s;
                }
            }

            return null;
        }
    }
}
