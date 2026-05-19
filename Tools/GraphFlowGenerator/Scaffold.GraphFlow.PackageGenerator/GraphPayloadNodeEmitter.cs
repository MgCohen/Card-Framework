using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPayloadNodeEmitter
    {
        internal static void Emit(SourceProductionContext spc, Compilation compilation, bool editorAssembly, GraphPackageModel package, CancellationToken ct, System.Collections.Generic.List<string>? editorRegistrations = null)
        {
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, package.RunnerFullyQualified);
            if (runner == null)
            {
                return;
            }

            var markerNs = string.IsNullOrEmpty(package.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : package.GraphFrameworkNamespace;
            var iEntry = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry");
            var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
            var iExec = compilation.GetTypeByMetadataName(markerNs + ".IExecutable`1");
            // Mode-2 commands now discovered by walking the package's CommandBase (decision #2).
            // [GraphCommandPair] attribute deleted in phase 4.
            var openCmdBase = package.CommandBaseMetadataName != null
                ? compilation.GetTypeByMetadataName(package.CommandBaseMetadataName)
                : null;
            var graphPortAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortAttribute");
            var graphPortIgnoreAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortIgnoreAttribute");
            var inAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.InAttribute");
            var outAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.OutAttribute");
            if (iEntry == null || iAction == null || graphPortAttr == null)
            {
                return;
            }

            var convention = package.Convention;

            var actionIface = iAction.Construct(runner);
            var execIface = iExec?.Construct(runner);
            var payloadAssembly = runner.ContainingAssembly;
            if (payloadAssembly is null)
            {
                return;
            }

            foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAssembly, ct))
            {
                ct.ThrowIfCancellationRequested();

                if (!PayloadDiscovery.IsCandidateType(type))
                {
                    continue;
                }

                if (PayloadDiscovery.IsGraphEntry(type, iEntry))
                {
                    // Implementing IGraphEntry is the sole opt-in marker — no separate attribute
                    // required. Filtering already excludes abstract / non-public types via
                    // PayloadDiscovery.IsCandidateType.
                    if (editorAssembly)
                    {
                        EmitEntryEditor(spc, type, compilation, graphPortAttr, graphPortIgnoreAttr, convention);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildEntryRegistration(type, package, compilation, graphPortAttr, graphPortIgnoreAttr));
                        }
                    }
                    else
                    {
                        EmitEntryRuntime(spc, type, package.RunnerTypeName, graphPortAttr, graphPortIgnoreAttr, convention);
                    }

                    continue;
                }

                // Mode 2 command/result pairs — discover by walking the type's base chain for the
                // package's CommandBase. Result type read from the closed Command<TResult> base.
                // No attribute required.
                if (openCmdBase != null && package.DispatcherBaseMetadataName != null
                    && PayloadDiscovery.TryGetCommandResultTypeFromBase(type, openCmdBase, out var resultType))
                {
                    var openDisp = compilation.GetTypeByMetadataName(package.DispatcherBaseMetadataName);
                    if (openDisp is not INamedTypeSymbol { TypeParameters.Length: 2 } openNamed)
                    {
                        continue;
                    }

                    var closedDisp = openNamed.Construct(type, resultType!);
                    if (editorAssembly)
                    {
                        EmitCommandEditor(spc, type, resultType, compilation, graphPortAttr, graphPortIgnoreAttr, convention);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildCommandRegistration(type, resultType, package, compilation, graphPortAttr, graphPortIgnoreAttr));
                        }
                    }
                    else
                    {
                        EmitCommandRuntime(
                            spc,
                            type,
                            resultType,
                            closedDisp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            package.RunnerTypeName,
                            runner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            graphPortAttr,
                            graphPortIgnoreAttr,
                            convention);
                    }

                    continue;
                }

                if (!PayloadDiscovery.Implements(type, actionIface))
                {
                    continue;
                }

                if (execIface != null && PayloadDiscovery.Implements(type, execIface))
                {
                    var (inputs, _) = FieldClassifier.Classify(type, package.Convention, inAttr, outAttr);
                    if (editorAssembly)
                    {
                        EmitExecutableEditor(spc, type, inputs, compilation);
                        if (editorRegistrations != null)
                        {
                            editorRegistrations.Add(BuildExecutableActionRegistration(type, inputs, package, compilation, graphPortAttr, graphPortIgnoreAttr));
                        }
                    }
                    else
                    {
                        EmitExecutableRuntime(spc, type, inputs, package.RunnerTypeName, graphPortAttr, graphPortIgnoreAttr, convention);
                    }

                    continue;
                }

                if (editorAssembly)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFG007_NoExecutionPath, Diagnostics.LocationOf(type), type.Name, package.RunnerTypeName));
                }
            }
        }

        static System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> FieldsWithPort(INamedTypeSymbol payload, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr, int convention)
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (PayloadDiscovery.IsPortField(f, convention, graphPortAttr, graphPortIgnoreAttr))
                {
                    result.Add((f.Name, TypeFmt.Simple(f.Type)));
                }
            }

            return result;
        }

        static string BuildEntryRegistration(INamedTypeSymbol payload, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr)
        {
            var leaf = payload.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + leaf;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "Runtime" : typeNs + "." + leaf + "Runtime";
            var payloadTypeFq = string.IsNullOrEmpty(typeNs) ? leaf : typeNs + "." + leaf;
            var runnerFq = GraphCompilationNames.TrimGlobal(package.RunnerFullyQualified);
            var fields = FieldsWithPort(payload, graphPortAttr, graphPortIgnoreAttr, package.Convention);
            var dataOuts = new System.Collections.Generic.List<string>();
            dataOuts.Add("Payload");
            foreach (var f in fields)
            {
                dataOuts.Add(f.Name);
            }

            return GraphRegistryEmitter.BuildEntryRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, payloadTypeFq, "FlowOut", dataOuts, null);
        }

        static string BuildExecutableActionRegistration(INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr)
        {
            var leaf = payload.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + CleanMenuName(leaf);
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "DispatcherRuntime" : typeNs + "." + leaf + "DispatcherRuntime";
            var runnerFq = GraphCompilationNames.TrimGlobal(package.RunnerFullyQualified);
            var dataIns = FieldsWithPortFiltered(inputs, graphPortAttr, graphPortIgnoreAttr, package.Convention);
            return GraphRegistryEmitter.BuildExecutableActionRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, "FlowIn", dataIns);
        }

        static System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> FieldsWithPortFiltered(System.Collections.Generic.IReadOnlyList<IFieldSymbol> fields, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr, int convention)
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var f in fields)
            {
                if (PayloadDiscovery.IsPortField(f, convention, graphPortAttr, graphPortIgnoreAttr))
                {
                    result.Add((f.Name, TypeFmt.Simple(f.Type)));
                }
            }

            return result;
        }

        static string BuildCommandRegistration(INamedTypeSymbol cmd, INamedTypeSymbol result, GraphPackageModel package, Compilation compilation, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr)
        {
            var leaf = cmd.Name;
            var editorTypeFq = GraphCompilationNames.EditorGraphToolkitNamespace(compilation) + "." + CleanMenuName(leaf);
            var typeNs = cmd.ContainingNamespace.IsGlobalNamespace ? "" : cmd.ContainingNamespace.ToDisplayString();
            var runtimeTypeFq = string.IsNullOrEmpty(typeNs) ? leaf + "DispatcherRuntime" : typeNs + "." + leaf + "DispatcherRuntime";
            var runnerFq = GraphCompilationNames.TrimGlobal(package.RunnerFullyQualified);
            var dataIns = FieldsWithPort(cmd, graphPortAttr, graphPortIgnoreAttr, package.Convention);
            var resultFields = FieldsWithPort(result, graphPortAttr, graphPortIgnoreAttr, package.Convention);
            var dataOuts = new System.Collections.Generic.List<string>();
            foreach (var f in resultFields)
            {
                dataOuts.Add(f.Name);
            }

            return GraphRegistryEmitter.BuildCommandRegistrationBlock(runnerFq, editorTypeFq, runtimeTypeFq, "FlowIn", "FlowOut", dataIns, dataOuts);
        }

        static void EmitEntryEditor(SourceProductionContext spc, INamedTypeSymbol payload, Compilation compilation, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr, int convention)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            var portFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (PayloadDiscovery.IsPortField(f, convention, graphPortAttr, graphPortIgnoreAttr))
                {
                    portFields.Add(f);
                }
            }
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf} : Node");
            sb.AppendLine("    {");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddOutputPort(\"FlowOut\")");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");

            // Whole-payload output (D8) — typed because the entry knows TPayload at gen time.
            var payloadFq = GraphCompilationNames.TrimGlobal(payload.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            sb.AppendLine($"            context.AddOutputPort<{payloadFq}>(\"Payload\").Build();");

            foreach (var f in portFields)
            {
                var gt = EditorGtPortMethod(f.Type);
                sb.AppendLine($"            context.AddOutputPort{gt}(\"{f.Name}\").Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string EditorGtPortMethod(ITypeSymbol t) => TypeFmt.IsInt(t) ? "<int>" : TypeFmt.IsString(t) ? "<string>" : "";

        // Strip suffixes that just clutter the editor menu name. "Command" stripped because
        // the user explicitly chose `DealDamage` over `DealDamageCommand`. "EditorNode" /
        // "Dispatcher" because they're framework boilerplate not domain meaning.
        static string CleanMenuName(string raw)
        {
            string Strip(string s, string suffix) =>
                s.EndsWith(suffix, System.StringComparison.Ordinal) && s.Length > suffix.Length
                    ? s.Substring(0, s.Length - suffix.Length)
                    : s;
            var name = raw;
            name = Strip(name, "EditorNode");
            name = Strip(name, "Dispatcher");
            name = Strip(name, "Command");
            return name;
        }

        static void EmitEntryRuntime(SourceProductionContext spc, INamedTypeSymbol payload, string runnerName, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr, int convention)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var fields = FieldsWithPort(payload, graphPortAttr, graphPortIgnoreAttr, convention);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}Runtime : EntryRuntimeNode<{leaf}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public FlowOutPort FlowOut;");
            foreach (var f in fields)
            {
                sb.AppendLine($"        public OutputPort<{f.CSharpType}> {f.Name};");
            }

            sb.AppendLine();
            sb.AppendLine($"        public {leaf}Runtime()");
            sb.AppendLine("        {");
            sb.AppendLine($"            FlowOut = new FlowOutPort(this, nameof(FlowOut));");
            foreach (var f in fields)
            {
                sb.AppendLine($"            {f.Name} = new OutputPort<{f.CSharpType}>(flow => PayloadOf(flow).{f.Name});");
            }

            sb.AppendLine($"            Ports.Add(FlowOut.Name, FlowOut);");
            foreach (var f in fields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", {f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}Runtime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        static ITypeSymbol GetFieldType(INamedTypeSymbol type, string fieldName)
        {
            foreach (var f in PayloadDiscovery.InstanceFields(type))
            {
                if (f.Name == fieldName)
                {
                    return f.Type;
                }
            }

            throw new System.InvalidOperationException($"Field {fieldName} not found on {type.Name}.");
        }

        static void EmitExecutableEditor(SourceProductionContext spc, INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, Compilation compilation)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var menuName = CleanMenuName(leaf);
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {menuName} : Node");
            sb.AppendLine("    {");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddInputPort(\"FlowIn\")");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            foreach (var f in inputs)
            {
                var gm = EditorGtInputPort(f.Type);
                sb.AppendLine($"            context.AddInputPort{gm}(\"{f.Name}\").Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{menuName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string EditorGtInputPort(ITypeSymbol t) => TypeFmt.IsInt(t) ? "<int>" : TypeFmt.IsString(t) ? "<string>" : "";

        static void EmitExecutableRuntime(SourceProductionContext spc, INamedTypeSymbol payload, System.Collections.Generic.IReadOnlyList<IFieldSymbol> inputs, string runnerName, INamedTypeSymbol graphPortAttr, INamedTypeSymbol? graphPortIgnoreAttr, int convention)
        {
            var sb = new StringBuilder();
            var leaf = payload.Name;
            var typeNs = payload.ContainingNamespace.IsGlobalNamespace ? "" : payload.ContainingNamespace.ToDisplayString();
            var inputFields = FieldsWithPortFiltered(inputs, graphPortAttr, graphPortIgnoreAttr, convention);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}DispatcherRuntime : RuntimeNode<{runnerName}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public FlowInPort FlowIn;");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"        public InputPort<{f.CSharpType}> {f.Name};");
            }

            sb.AppendLine();
            sb.AppendLine($"        public {leaf}DispatcherRuntime()");
            sb.AppendLine("        {");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"            {f.Name} = new InputPort<{f.CSharpType}>();");
            }

            sb.AppendLine($"            FlowIn = FlowInPort.Async(this, nameof(FlowIn), async flow =>");
            sb.AppendLine("            {");
            sb.AppendLine($"                var payload = new {leaf}");
            sb.AppendLine("                {");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"                    {f.Name} = {f.Name}.Read(flow),");
            }

            sb.AppendLine("                };");
            sb.AppendLine("                await payload.Execute(Runner(flow)).ConfigureAwait(false);");
            sb.AppendLine("                return null;");
            sb.AppendLine("            });");
            sb.AppendLine($"            Ports.Add(FlowIn.Name, FlowIn);");
            foreach (var f in inputFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", {f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void EmitCommandEditor(
            SourceProductionContext spc,
            INamedTypeSymbol cmd,
            INamedTypeSymbol result,
            Compilation compilation,
            INamedTypeSymbol graphPortAttr,
            INamedTypeSymbol? graphPortIgnoreAttr,
            int convention)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
            var menuName = CleanMenuName(leaf);
            var ns = GraphCompilationNames.EditorGraphToolkitNamespace(compilation);
            var cmdPortFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(cmd))
            {
                if (PayloadDiscovery.IsPortField(f, convention, graphPortAttr, graphPortIgnoreAttr))
                {
                    cmdPortFields.Add(f);
                }
            }
            var resultPortFields = new System.Collections.Generic.List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(result))
            {
                if (PayloadDiscovery.IsPortField(f, convention, graphPortAttr, graphPortIgnoreAttr))
                {
                    resultPortFields.Add(f);
                }
            }
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {menuName} : Node");
            sb.AppendLine("    {");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnDefinePorts(IPortDefinitionContext context)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.AddInputPort(\"FlowIn\")");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            sb.AppendLine("            context.AddOutputPort(\"FlowOut\")");
            sb.AppendLine("                .WithDisplayName(string.Empty)");
            sb.AppendLine("                .WithConnectorUI(PortConnectorUI.Arrowhead)");
            sb.AppendLine("                .Build();");
            foreach (var f in cmdPortFields)
            {
                var gm = EditorGtInputPort(f.Type);
                sb.AppendLine($"            context.AddInputPort{gm}(\"{f.Name}\").Build();");
            }

            foreach (var f in resultPortFields)
            {
                var om = EditorGtPortMethod(f.Type);
                sb.AppendLine($"            context.AddOutputPort{om}(\"{f.Name}\").Build();");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{menuName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void EmitCommandRuntime(
            SourceProductionContext spc,
            INamedTypeSymbol cmd,
            INamedTypeSymbol result,
            string closedDispatcherFq,
            string runnerName,
            string runnerFq,
            INamedTypeSymbol graphPortAttr,
            INamedTypeSymbol? graphPortIgnoreAttr,
            int convention)
        {
            var sb = new StringBuilder();
            var leaf = cmd.Name;
            var typeNs = cmd.ContainingNamespace.IsGlobalNamespace ? "" : cmd.ContainingNamespace.ToDisplayString();
            var cmdFields = FieldsWithPort(cmd, graphPortAttr, graphPortIgnoreAttr, convention);
            var resultFields = FieldsWithPort(result, graphPortAttr, graphPortIgnoreAttr, convention);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine();
            sb.AppendLine($"namespace {typeNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public sealed class {leaf}DispatcherRuntime : {closedDispatcherFq}");
            sb.AppendLine("    {");
            foreach (var f in cmdFields)
            {
                sb.AppendLine($"        public {f.CSharpType} {f.Name};");
                sb.AppendLine($"        public InputPort<{f.CSharpType}> In{f.Name};");
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"        public OutputPort<{f.CSharpType}> {f.Name};");
            }

            sb.AppendLine();
            sb.AppendLine($"        {result.Name}[] _results = Array.Empty<{result.Name}>();");
            sb.AppendLine();
            sb.AppendLine($"        public {leaf}DispatcherRuntime()");
            sb.AppendLine("        {");
            foreach (var f in cmdFields)
            {
                sb.AppendLine($"            In{f.Name} = new InputPort<{f.CSharpType}>();");
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"            {f.Name} = new OutputPort<{f.CSharpType}>(flow => _results[flow.Index].{f.Name});");
            }

            foreach (var f in cmdFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", In{f.Name});");
            }

            foreach (var f in resultFields)
            {
                sb.AppendLine($"            Ports.Add(\"{f.Name}\", {f.Name});");
            }

            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public override void Initialize({runnerFq} runner)");
            sb.AppendLine("        {");
            sb.AppendLine("            base.Initialize(runner);");
            sb.AppendLine($"            _results = new {result.Name}[runner.MaxConcurrentFlows];");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        protected override {cmd.Name} BuildPayload(Flow flow) => new {cmd.Name} {{ {BuildPayloadInputs(cmdFields)} }};");
            sb.AppendLine();
            sb.AppendLine($"        protected override void WriteOutputs(Flow flow, {result.Name} result) => _results[flow.Index] = result;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource($"{leaf}DispatcherRuntime.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static string BuildPayloadInputs(System.Collections.Generic.IReadOnlyList<(string Name, string CSharpType)> cmdFields)
        {
            var sb = new StringBuilder();
            foreach (var f in cmdFields)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append($"{f.Name} = In{f.Name}.IsConnected ? In{f.Name}.Read(flow) : {f.Name}");
            }

            return sb.ToString();
        }
    }

    internal static class PayloadDiscovery
    {
        internal static bool IsCandidateType(INamedTypeSymbol type) =>
            (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) && type.DeclaredAccessibility == Accessibility.Public;

        internal static bool IsGraphEntry(INamedTypeSymbol type, INamedTypeSymbol iEntry)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, iEntry))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool Implements(INamedTypeSymbol type, INamedTypeSymbol ifaceConstructed)
        {
            foreach (var i in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(i, ifaceConstructed))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Walk the type's base chain for the package's open CommandBase generic; if found, extract
        /// TResult from the closed base. No attribute required (decision #2).
        /// </summary>
        internal static bool TryGetCommandResultTypeFromBase(
            INamedTypeSymbol type,
            INamedTypeSymbol openCommandBase,
            out INamedTypeSymbol? resultType)
        {
            for (var b = type.BaseType; b != null; b = b.BaseType)
            {
                if (!b.IsGenericType) continue;
                if (!SymbolEqualityComparer.Default.Equals(b.OriginalDefinition, openCommandBase)) continue;
                if (b.TypeArguments.Length != 1) continue;
                if (b.TypeArguments[0] is INamedTypeSymbol r)
                {
                    resultType = r;
                    return true;
                }
            }

            resultType = null;
            return false;
        }

        internal static ImmutableArray<IFieldSymbol> InstanceFields(INamedTypeSymbol type)
        {
            var b = ImmutableArray.CreateBuilder<IFieldSymbol>();
            foreach (var m in type.GetMembers())
            {
                if (m is IFieldSymbol f && !f.IsStatic && f.DeclaredAccessibility == Accessibility.Public)
                {
                    b.Add(f);
                }
            }

            return b.ToImmutable();
        }

        internal static bool HasGraphPort(IFieldSymbol field, INamedTypeSymbol graphPortAttr)
        {
            foreach (var a in field.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphPortAttr))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool HasGraphPortIgnore(IFieldSymbol field, INamedTypeSymbol? graphPortIgnoreAttr)
        {
            if (graphPortIgnoreAttr == null)
            {
                return false;
            }

            foreach (var a in field.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphPortIgnoreAttr))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Convention-aware port predicate. Under <c>AllFieldsIn</c> every public instance field is
        /// a port unless tagged <c>[GraphPortIgnore]</c>. Under <c>AttributedFields</c> only
        /// <c>[GraphPort]</c>-tagged fields are ports. Other conventions fall back to the strict
        /// attributed shape.
        /// </summary>
        internal static bool IsPortField(
            IFieldSymbol field,
            int convention,
            INamedTypeSymbol graphPortAttr,
            INamedTypeSymbol? graphPortIgnoreAttr)
        {
            if (convention == PortConvention.AllFieldsIn)
            {
                return !HasGraphPortIgnore(field, graphPortIgnoreAttr);
            }

            return HasGraphPort(field, graphPortAttr);
        }

        internal static bool HasGraphEventAttribute(INamedTypeSymbol type, INamedTypeSymbol graphEventAttr)
        {
            foreach (var a in type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphEventAttr))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class TypeFmt
    {
        internal static bool IsInt(ITypeSymbol t) => t.SpecialType == SpecialType.System_Int32;

        internal static bool IsString(ITypeSymbol t) => t.SpecialType == SpecialType.System_String;

        internal static string Simple(ITypeSymbol t)
        {
            if (IsInt(t))
            {
                return "int";
            }

            if (IsString(t))
            {
                return "string";
            }

            return t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        internal static string DefaultLiteral(ITypeSymbol t)
        {
            if (IsInt(t))
            {
                return "0";
            }

            if (IsString(t))
            {
                return "\"\"";
            }

            return "default!";
        }
    }
}
