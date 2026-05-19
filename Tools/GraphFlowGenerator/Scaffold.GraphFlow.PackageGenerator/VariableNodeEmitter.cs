using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class VariableNodeEmitter
    {
        internal static void AppendRegistrations(
            List<string> registrations,
            Compilation compilation,
            GraphPackageModel package,
            ImmutableArray<VariableTypeInfo> variableTypes)
        {
            if (variableTypes.IsDefaultOrEmpty) return;

            registrations.Add(BuildRegistrationBlock(compilation, package, "Get", "GetVariable", variableTypes));
            registrations.Add(BuildRegistrationBlock(compilation, package, "Set", "SetVariable", variableTypes));
            registrations.Add(BuildRegistrationBlock(compilation, package, "Observe", "ObserveVariable", variableTypes));
        }

        static string BuildRegistrationBlock(
            Compilation compilation,
            GraphPackageModel package,
            string kind,
            string editorName,
            ImmutableArray<VariableTypeInfo> variableTypes)
        {
            var runnerFq = GraphCompilationNames.TrimGlobal(package.RunnerFullyQualified);
            var registryNs = GraphRegistryEmitter.ResolveRegistryNamespace(package, compilation);
            var editorTypeFq = registryNs + "." + editorName;
            var catalogNs = GraphCatalogEmitter.ResolveCatalogNamespace(package, compilation);
            var catalogTy = package.GraphStem + GraphCatalogEmitter.CatalogClassSuffix;
            var enumFq = catalogNs + "." + catalogTy + ".VariableType";

            var sb = new StringBuilder();
            sb.AppendLine($"            r.Register(new {GraphRegistryEmitter.EditorRegistryTypeName}<{runnerFq}>.NodeRegistration");
            sb.AppendLine("            {");
            sb.AppendLine($"                EditorNodeType = typeof({editorTypeFq}),");

            sb.AppendLine("                Factory = node =>");
            sb.AppendLine("                {");
            sb.AppendLine($"                    var typed = ({editorTypeFq})node;");
            sb.AppendLine($"                    var opt = typed.GetNodeOptionByName(\"VariableType\");");
            sb.AppendLine($"                    if (opt == null || !opt.TryGetValue<{enumFq}>(out var picked) || picked == {enumFq}.None)");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        throw new System.InvalidOperationException(\"{editorName} node has no VariableType selected.\");");
            sb.AppendLine("                    }");
            sb.AppendLine("                    switch (picked)");
            sb.AppendLine("                    {");

            const string runtimeNs = "Scaffold.GraphFlow.Nodes";
            foreach (var info in variableTypes)
            {
                var runtimeClass = $"{runtimeNs}.{kind}Variable<{info.ValueTypeSimple}>";
                sb.AppendLine($"                        case {enumFq}.{info.Label}:");
                sb.AppendLine($"                            return new {runtimeClass}();");
            }

            sb.AppendLine("                        default:");
            sb.AppendLine($"                            throw new System.InvalidOperationException($\"{editorName}: unsupported VariableType '{{picked}}'.\");");
            sb.AppendLine("                    }");
            sb.AppendLine("                },");

            switch (kind)
            {
                case "Get":
                    AppendPortSet(sb, "DataOutputPortNames", new[] { "Value" });
                    break;
                case "Set":
                    AppendPortSet(sb, "FlowInputPortNames", new[] { "In" });
                    AppendPortSet(sb, "FlowOutputPortNames", new[] { "Done" });
                    AppendPortSet(sb, "DataInputPortNames", new[] { "NewValue" });
                    break;
                case "Observe":
                    AppendPortSet(sb, "FlowOutputPortNames", new[] { "FlowOut" });
                    AppendPortSet(sb, "DataOutputPortNames", new[] { "NewValue" });
                    break;
            }

            sb.AppendLine("            });");
            return sb.ToString();
        }

        static void AppendPortSet(StringBuilder sb, string fieldName, string[] names)
        {
            sb.AppendLine($"                {fieldName} = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)");
            sb.AppendLine("                {");
            foreach (var name in names)
                sb.AppendLine($"                    \"{name}\",");
            sb.AppendLine("                },");
        }
    }
}
