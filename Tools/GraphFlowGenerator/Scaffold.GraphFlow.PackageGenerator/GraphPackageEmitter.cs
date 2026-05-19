using System.Threading;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scaffold.GraphFlow.PackageGenerator
{
    internal static class GraphPackageEmitter
    {
        internal static void Emit(SourceProductionContext spc, Compilation compilation, ImmutableArray<GraphPackageModel> packages, CancellationToken cancellationToken)
        {
            if (packages.IsDefaultOrEmpty)
            {
                return;
            }

            var editorAsm = GraphCompilationNames.IsEditorAssembly(compilation);
            var ordered = packages.Sort(static (a, b) => string.CompareOrdinal(a.RunnerFullyQualified, b.RunnerFullyQualified));
            if (editorAsm && ordered.Length > 1)
            {
                ReportMultiPackageBindings(spc, compilation, ordered, cancellationToken);
            }

            foreach (var p in ordered)
            {
                DispatchOnePackage(spc, compilation, editorAsm, p, cancellationToken);
            }

            // Whole-compilation lint — flags any RuntimeNode / GraphToolkit Node derivative missing
            // [Serializable]. Runs once per pass; runtime and editor asms each see their own source
            // types so there's no double-reporting.
            SerializableLintPass.Run(spc, compilation, cancellationToken);
        }

        // EFG008: a payload that satisfies IGraphAction<R1> AND IGraphAction<R2> for two declared packages is ambiguous.
        // Reported once from the editor pass (avoids duplicate diagnostics across runtime+editor compiles).
        // (D5: entries are no longer runner-typed and can't be per-package-ambiguous; only IGraphAction<R> stays runner-typed.)
        static void ReportMultiPackageBindings(SourceProductionContext spc, Compilation compilation, ImmutableArray<GraphPackageModel> packages, CancellationToken ct)
        {
            var perRunner = new System.Collections.Generic.List<(string runner, INamedTypeSymbol actionIface, IAssemblySymbol asm)>();
            foreach (var p in packages)
            {
                var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
                if (runner?.ContainingAssembly == null)
                {
                    continue;
                }

                var markerNs = string.IsNullOrEmpty(p.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : p.GraphFrameworkNamespace;
                var iAction = compilation.GetTypeByMetadataName(markerNs + ".IGraphAction`1");
                if (iAction == null)
                {
                    continue;
                }

                perRunner.Add((p.RunnerTypeName, iAction.Construct(runner), runner.ContainingAssembly));
            }

            // Collect distinct payload assemblies once so we don't walk the same assembly N times.
            var assemblies = new System.Collections.Generic.HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            foreach (var entry in perRunner)
            {
                assemblies.Add(entry.asm);
            }

            foreach (var asm in assemblies.OrderBy(a => a.Name, System.StringComparer.Ordinal))
            {
                foreach (var type in GraphPayloadTypeWalker.AllNamedTypesInAssembly(asm, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!PayloadDiscovery.IsCandidateType(type))
                    {
                        continue;
                    }

                    string? firstRunner = null;
                    foreach (var (runnerName, actionIface, _) in perRunner)
                    {
                        if (!PayloadDiscovery.Implements(type, actionIface))
                        {
                            continue;
                        }

                        if (firstRunner == null)
                        {
                            firstRunner = runnerName;
                            continue;
                        }

                        spc.ReportDiagnostic(Diagnostic.Create(Diagnostics.EFG008_MultiPackageBinding, Diagnostics.LocationOf(type), type.Name, firstRunner, runnerName));
                        break;
                    }
                }
            }
        }

        static void DispatchOnePackage(SourceProductionContext spc, Compilation compilation, bool editorAsm, GraphPackageModel p, CancellationToken cancellationToken)
        {
            // The GraphAsset SO is intentionally NOT emitted — it must be hand-written by the consumer.
            // Unity's MonoScript binding (used by AssetImportContext.AddObjectToAsset) only resolves ScriptableObject types
            // declared in a real .cs file with matching name; generator-emitted virtual files do not satisfy that lookup.
            if (editorAsm)
            {
                AddEditorSources(spc, compilation, p);
                var registrations = new System.Collections.Generic.List<string>();
                GraphPayloadNodeEmitter.Emit(spc, compilation, true, p, cancellationToken, registrations);
                var (_, variableTypes) = EmitGenericNodeArtifacts(spc, compilation, p, registrations, cancellationToken, editorAssembly: true);
                if (variableTypes.Length > 0)
                {
                    VariableNodeEmitter.AppendRegistrations(registrations, compilation, p, variableTypes);
                }
                GraphRegistryEmitter.EmitRegistryFile(spc, compilation, p, registrations);
                // Per-package field-level lint (EFG002/003/004/006). Editor pass only — payloads
                // are reachable via the runner's containing asm and reporting once per pass keeps
                // the diagnostic deduplicated across runtime + editor compiles.
                PayloadFieldLintPass.Run(spc, compilation, p, cancellationToken);
            }
            else
            {
                GraphPayloadNodeEmitter.Emit(spc, compilation, false, p, cancellationToken);
                var (_, variableTypes) = EmitGenericNodeArtifacts(spc, compilation, p, registrations: null, cancellationToken, editorAssembly: false);
                EmitCatalogIfRunnerAsm(spc, compilation, p, variableTypes, cancellationToken);
            }
        }

        /// <summary>
        /// Emit the per-package <c>&lt;Stem&gt;Catalog</c> into the runner's containing asm (the
        /// runtime asm). The catalog has no editor dependencies — factories close over runtime
        /// types only — so it lives next to the runtime nodes it describes. Editor mirrors and
        /// the registry read it via the asm reference.
        /// </summary>
        static void EmitCatalogIfRunnerAsm(SourceProductionContext spc, Compilation compilation, GraphPackageModel p, ImmutableArray<VariableTypeInfo> variableTypes, CancellationToken ct)
        {
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
            var payloadAsm = runner?.ContainingAssembly;
            if (payloadAsm == null) return;
            // Only emit catalog when the current compilation IS the runner's asm — otherwise we'd
            // try to emit duplicate definitions across every consumer asm.
            if (!SymbolEqualityComparer.Default.Equals(compilation.Assembly, payloadAsm)) return;

            var graphEventAttr      = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphEventAttribute");
            var graphReturnTypeAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphReturnTypeAttribute");
            var graphPortAttr       = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortAttribute");
            var graphPortIgnoreAttr = compilation.GetTypeByMetadataName("Scaffold.GraphFlow.GraphPortIgnoreAttribute");
            var markerNs = string.IsNullOrEmpty(p.GraphFrameworkNamespace) ? "Scaffold.GraphFlow" : p.GraphFrameworkNamespace;
            var iEntry  = compilation.GetTypeByMetadataName(markerNs + ".IGraphEntry");

            if (graphEventAttr == null || graphPortAttr == null || iEntry == null) return;

            // Walk the payload asm once and reuse for every Discover* — each used to walk the
            // same asm itself, producing four full type enumerations.
            var allTypes = GraphPayloadTypeWalker.AllNamedTypesInAssembly(payloadAsm, ct);

            var events   = GraphCatalogDiscovery.DiscoverEvents(compilation, p, allTypes, graphEventAttr, ct);
            var commands = GraphCatalogDiscovery.DiscoverCommands(compilation, p, allTypes, graphPortAttr, graphPortIgnoreAttr, ct);
            var entries  = GraphCatalogDiscovery.DiscoverEntries(compilation, p, allTypes, iEntry, graphPortAttr, graphPortIgnoreAttr, ct);
            var returns  = GraphCatalogDiscovery.DiscoverReturns(compilation, p, allTypes, graphReturnTypeAttr, ct);

            var variables = new System.Collections.Generic.List<GraphCatalogEmitter.VariableDescriptor>();
            foreach (var v in variableTypes)
                variables.Add(new GraphCatalogEmitter.VariableDescriptor(v.Label, v.ValueTypeFq));

            GraphCatalogEmitter.EmitRuntimeCatalog(spc, compilation, p, events, commands, entries, returns, variables);
        }

        static (ImmutableArray<GenericNodeModel> nodes, ImmutableArray<VariableTypeInfo> variableTypes) EmitGenericNodeArtifacts(
            SourceProductionContext spc,
            Compilation compilation,
            GraphPackageModel p,
            System.Collections.Generic.List<string>? registrations,
            System.Threading.CancellationToken ct,
            bool editorAssembly)
        {
            // D6: walk every assembly that references the GraphFlow runtime asm, plus the runner's
            // own asm, plus the current compilation's asm. The "references the GraphFlow runtime"
            // filter is the firewall — only asms that already reference the package can possibly
            // *define* [GraphNode]-attributed types, so this excludes Unity engine asms / 3rd-party
            // SDKs without any consumer ceremony. Built-in nodes in the package light up
            // automatically (the package runtime asm references itself trivially).
            var runner = GraphCompilationNames.TypeFromFullyQualified(compilation, p.RunnerFullyQualified);
            var runnerAsm = runner?.ContainingAssembly;
            if (runnerAsm == null)
            {
                return (ImmutableArray<GenericNodeModel>.Empty, ImmutableArray<VariableTypeInfo>.Empty);
            }

            const string GraphFlowRuntimeAsmName = "Scaffold.GraphFlow";
            var seen = new System.Collections.Generic.HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            var asms = new System.Collections.Generic.List<IAssemblySymbol>();

            void Consider(IAssemblySymbol asm)
            {
                if (asm == null || !seen.Add(asm)) return;
                // Always include runnerAsm and the current compilation asm (which IS the runner asm
                // for the package's own consumers), plus the package runtime asm itself. For other
                // asms, only include them if they reference Scaffold.GraphFlow.
                if (SymbolEqualityComparer.Default.Equals(asm, runnerAsm) ||
                    SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly) ||
                    asm.Name == GraphFlowRuntimeAsmName)
                {
                    asms.Add(asm);
                    return;
                }
                foreach (var refName in asm.Modules)
                {
                    foreach (var refAsm in refName.ReferencedAssemblySymbols)
                    {
                        if (refAsm.Name == GraphFlowRuntimeAsmName)
                        {
                            asms.Add(asm);
                            return;
                        }
                    }
                }
            }

            Consider(compilation.Assembly);
            Consider(runnerAsm);
            foreach (var refAsm in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                Consider(refAsm);
            }

            // Aggregate + deduplicate parsed nodes across asms. Keys are (ns, type-name) since the
            // type-name is unique within the package built-ins / consumer asms in practice. We tag
            // each model with whether it was sourced from the *current* compilation so the runtime
            // emit pass only emits the partial-class completion in the asm where the source actually
            // lives — emitting it into a consumer asm declares a *new* (empty) class in that asm.
            var dedupe = new System.Collections.Generic.HashSet<string>();
            var allNodes = ImmutableArray.CreateBuilder<GenericNodeModel>();
            var inCurrentCompilation = new System.Collections.Generic.HashSet<string>();
            var varDedup = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var allVarTypes = ImmutableArray.CreateBuilder<VariableTypeInfo>();
            foreach (var asm in asms.OrderBy(a => a.Name, System.StringComparer.Ordinal))
            {
                // Walk the assembly's types once; feed both GenericNodeParser and VariableDiscovery.
                var allTypes = GraphPayloadTypeWalker.AllNamedTypesInAssembly(asm, ct);
                var parsed = GenericNodeParser.Parse(compilation, allTypes, ct);
                var varInfos = VariableDiscovery.DiscoverFromTypes(compilation, allTypes, ct);

                var isCurrent = SymbolEqualityComparer.Default.Equals(asm, compilation.Assembly);
                foreach (var n in parsed)
                {
                    var key = (n.TypeNamespace ?? "") + "." + n.TypeName;
                    if (dedupe.Add(key))
                    {
                        allNodes.Add(n);
                        if (isCurrent) inCurrentCompilation.Add(key);
                    }
                }

                foreach (var v in varInfos)
                {
                    if (varDedup.Add(v.Label))
                        allVarTypes.Add(v);
                }
            }

            var built = allNodes.ToImmutable();
            GraphGenericNodeEmitter.EmitForPackage(spc, compilation, p, built, inCurrentCompilation, registrations, editorAssembly);

            allVarTypes.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return (built, allVarTypes.ToImmutable());
        }

        static void AddEditorSources(SourceProductionContext spc, Compilation compilation, GraphPackageModel p)
        {
            var graphSb = new StringBuilder();
            AppendEditorGraphFile(graphSb, compilation, p);
            var graphPath = $"{p.GraphStem}Graph.g.cs";
            var graphText = graphSb.ToString();
            var graphSource = SourceText.From(graphText, Encoding.UTF8);
            spc.AddSource(graphPath, graphSource);
            // Importer is NOT source-generated — it must be a hand-written .cs file so
            // Unity assigns a stable script GUID for the .meta ScriptedImporter reference.
            // Source-generated scripts land in obj/Generated/ with no .cs.meta, which causes
            // the .card meta file to get script:{instanceID:0} → broken Inspector display.
            var valSb = new StringBuilder();
            AppendEditorGraphValidationFile(valSb, compilation, p);
            var valPath = $"{p.GraphStem}GraphValidation.g.cs";
            spc.AddSource(valPath, SourceText.From(valSb.ToString(), Encoding.UTF8));

            var trigSb = new StringBuilder();
            AppendOnTriggerEditorShimFile(trigSb, compilation, p);
            spc.AddSource($"{p.GraphStem}_OnTrigger.g.cs", SourceText.From(trigSb.ToString(), Encoding.UTF8));

            var retSb = new StringBuilder();
            AppendReturnEditorShimFile(retSb, compilation, p);
            spc.AddSource($"{p.GraphStem}_Return.g.cs", SourceText.From(retSb.ToString(), Encoding.UTF8));

            AppendVariableEditorShimFile(spc, compilation, p, "GetVariable",
                "global::Scaffold.GraphFlow.Editor.Nodes.GetVariableEditorNode",
                "GetValueType", "Type");
            AppendVariableEditorShimFile(spc, compilation, p, "SetVariable",
                "global::Scaffold.GraphFlow.Editor.Nodes.SetVariableEditorNode",
                "GetValueType", "Type");
            AppendVariableEditorShimFile(spc, compilation, p, "ObserveVariable",
                "global::Scaffold.GraphFlow.Editor.Nodes.ObserveVariableEditorNode",
                "GetValueType", "Type");

            // Inspector is also hand-written — a [CustomEditor] for the asset type
            // would override the ScriptedImporter inspector view (hiding import settings).
            // Without it, Unity's default ScriptedImporter inspector shows both import
            // settings and the imported object, matching official GT sample behavior.
        }

        /// <summary>
        /// Emit a per-package concrete-typed <c>[CustomEditor]</c> for the package's
        /// <c>&lt;Stem&gt;GraphAsset</c>. Concrete-type CustomEditors take precedence over
        /// <c>editorForChildClasses</c> matchers (NaughtyAttributes' <c>[CustomEditor(typeof(Object), true)]</c>),
        /// so this guarantees the framework's <see cref="GraphAssetEditor"/> renders the asset
        /// instead of NaughtyInspector — which trips on null SerializeReference entries and spams
        /// "target object is null" errors. Inheriting the framework editor keeps the rendering
        /// behavior identical.
        /// </summary>
        static void AppendConcreteAssetInspectorFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            var importerNs = EditorImporterNamespace(compilation);

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using UnityEditor;");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine();
            sb.AppendLine($"namespace {importerNs}");
            sb.AppendLine("{");
            sb.AppendLine($"    [CustomEditor(typeof({p.GraphStem}GraphAsset))]");
            sb.AppendLine($"    public sealed class {p.GraphStem}GraphAssetInspector : global::Scaffold.GraphFlow.Editor.GraphAssetEditor {{ }}");
            sb.AppendLine("}");
        }

        /// <summary>
        /// Emit the per-package OnTrigger editor shim — a 5-line concrete subclass that closes
        /// the generic <c>OnTriggerEditorNode&lt;TEnum&gt;</c> base over the catalog's
        /// <c>EventChoice</c> enum and forwards <c>GetPorts</c> into <c>&lt;Stem&gt;Catalog.Resolve(c).Ports</c>.
        /// <para>The shim must be per-package because GraphToolkit's <c>AddOption&lt;TEnum&gt;</c>
        /// dropdown reads the closed enum at compile time, and <c>[UseWithGraph(typeof(&lt;Stem&gt;Graph))]</c>
        /// scopes menu visibility to the right graph type.</para>
        /// </summary>
        static void AppendOnTriggerEditorShimFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            var registryNs = GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation);
            var catalogNs  = GraphCatalogEmitter.ResolveCatalogNamespace(p, compilation);
            var catalogTy  = p.GraphStem + GraphCatalogEmitter.CatalogClassSuffix;

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine("using Scaffold.GraphFlow.Editor.Nodes;");
            sb.AppendLine($"using {EditorGraphToolkitNamespace(compilation)};");
            sb.AppendLine($"using {catalogNs};");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {registryNs}");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    [UseWithGraph(typeof({p.GraphStem}Graph))]");
            sb.AppendLine($"    public sealed class OnTrigger : global::Scaffold.GraphFlow.Editor.Nodes.OnTriggerEditorNode<{catalogTy}.EventType>");
            sb.AppendLine("    {");
            sb.AppendLine($"        protected override IReadOnlyList<PortMeta>? GetPorts({catalogTy}.EventType picked)");
            sb.AppendLine($"            => {catalogTy}.Resolve(picked)?.Ports;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        /// <summary>
        /// Emit the per-package Return editor shim — closes the generic
        /// <c>ReturnEditorNode&lt;TEnum&gt;</c> base over the catalog's <c>ReturnChoice</c> enum
        /// and forwards <c>GetResultType</c> into <c>&lt;Stem&gt;Catalog.Resolve(c).Type</c>.
        /// </summary>
        static void AppendReturnEditorShimFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            var registryNs = GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation);
            var catalogNs  = GraphCatalogEmitter.ResolveCatalogNamespace(p, compilation);
            var catalogTy  = p.GraphStem + GraphCatalogEmitter.CatalogClassSuffix;

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Scaffold.GraphFlow.Editor.Nodes;");
            sb.AppendLine($"using {EditorGraphToolkitNamespace(compilation)};");
            sb.AppendLine($"using {catalogNs};");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {registryNs}");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    [UseWithGraph(typeof({p.GraphStem}Graph))]");
            sb.AppendLine($"    public sealed class Return : global::Scaffold.GraphFlow.Editor.Nodes.ReturnEditorNode<{catalogTy}.ReturnType>");
            sb.AppendLine("    {");
            sb.AppendLine($"        protected override Type? GetResultType({catalogTy}.ReturnType picked)");
            sb.AppendLine($"            => {catalogTy}.Resolve(picked)?.Type;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        static void AppendVariableEditorShimFile(
            SourceProductionContext spc,
            Compilation compilation,
            GraphPackageModel p,
            string editorName,
            string editorBaseFq,
            string abstractMethod,
            string resolverProperty)
        {
            var registryNs = GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation);
            var catalogNs  = GraphCatalogEmitter.ResolveCatalogNamespace(p, compilation);
            var catalogTy  = p.GraphStem + GraphCatalogEmitter.CatalogClassSuffix;
            var enumRef    = catalogTy + ".VariableType";

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using Scaffold.GraphFlow;");
            sb.AppendLine("using Scaffold.GraphFlow.Editor.Nodes;");
            sb.AppendLine($"using {EditorGraphToolkitNamespace(compilation)};");
            sb.AppendLine($"using {catalogNs};");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {registryNs}");
            sb.AppendLine("{");
            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    [UseWithGraph(typeof({p.GraphStem}Graph))]");
            sb.AppendLine($"    public sealed class {editorName} : {editorBaseFq}<{enumRef}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        protected override Type? {abstractMethod}({enumRef} picked)");
            sb.AppendLine($"            => {catalogTy}.Resolve(picked)?.{resolverProperty};");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource($"{p.GraphStem}_{editorName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        static void AppendEditorGraphValidationFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            // Per ExecPlan-v2 M2 milestone — initial OnGraphChanged rule set:
            //   EFG-V01 unsupported node type           (error)   — no registry entry; bake will fail.
            //   EFG-V02 duplicate entry                 (warning) — bake keeps the first occurrence.
            //   EFG-V03 edge port pairing mismatch      (error)   — flow↔data wire, or unknown port.
            //   EFG-V04 unterminated run-flow path      (warning) — flow output unconnected; path
            //                                                       does not reach a Return/Cancel/
            //                                                       ReturnBool terminator.
            //   EFG-V05 required-input-unwired          — deferred to M4 (needs [Required] metadata
            //                                             we haven't designed yet).
            //   EFG-V06 type-mismatch on data edges     — deferred; GraphToolkit refuses heterogeneous
            //                                             port wiring at the UI level so this is not
            //                                             reachable through the authoring surface today.
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine($"using {GraphRegistryEmitter.EditorRegistryNamespace};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine($"using {GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation)};");
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorGraphToolkitNamespace(compilation)}");
            sb.AppendLine("{");
            sb.AppendLine($"    partial class {p.GraphStem}Graph");
            sb.AppendLine("    {");
            sb.AppendLine("        public override void OnGraphChanged(GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            base.OnGraphChanged(logger);");
            sb.AppendLine($"            var registry = {GraphRegistryEmitter.RegistryTypeName(p)}.Instance;");
            sb.AppendLine("            var registrations = new Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration>();");
            sb.AppendLine("            var entriesByType = new Dictionary<string, List<INode>>();");
            sb.AppendLine("            foreach (var node in GetNodes())");
            sb.AppendLine("            {");
            sb.AppendLine("                if (node is IConstantNode || node is IVariableNode) continue;");
            sb.AppendLine("                var reg = registry.Lookup(node.GetType());");
            sb.AppendLine("                if (reg == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.LogError($\"[EFG-V01] Unsupported node type {node.GetType().Name} — no registry entry. Re-baking will fail.\", node);");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine("                registrations[node] = reg;");
            sb.AppendLine("                if (reg.EntryTypeId == null) continue;");
            sb.AppendLine("                if (!entriesByType.TryGetValue(reg.EntryTypeId, out var list))");
            sb.AppendLine("                {");
            sb.AppendLine("                    list = new List<INode>();");
            sb.AppendLine("                    entriesByType[reg.EntryTypeId] = list;");
            sb.AppendLine("                }");
            sb.AppendLine("                list.Add(node);");
            sb.AppendLine("            }");
            sb.AppendLine("            foreach (var kv in entriesByType)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (kv.Value.Count <= 1) continue;");
            sb.AppendLine("                foreach (var dup in kv.Value.Skip(1))");
            sb.AppendLine("                {");
            sb.AppendLine("                    logger.LogWarning($\"[EFG-V02] Duplicate entry node for '{kv.Key}'. The bake step keeps only the first occurrence.\", dup);");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            ValidateEdgePairings(registrations, logger);");
            sb.AppendLine("            ValidateFlowOutSingleConnection(registrations, logger);");
            sb.AppendLine("            ValidateRunFlowTerminates(registrations, logger);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void ValidateEdgePairings(Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration> registrations, GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            var connected = new List<IPort>();");
            sb.AppendLine("            foreach (var kv in registrations)");
            sb.AppendLine("            {");
            sb.AppendLine("                var fromNode = kv.Key;");
            sb.AppendLine("                var fromReg = kv.Value;");
            sb.AppendLine("                foreach (var port in fromNode.GetOutputPorts())");
            sb.AppendLine("                {");
            sb.AppendLine("                    var fromIsFlow = fromReg.FlowOutputPortNames.Contains(port.Name);");
            sb.AppendLine("                    var fromIsData = fromReg.DataOutputPortNames.Contains(port.Name);");
            sb.AppendLine("                    connected.Clear();");
            sb.AppendLine("                    port.GetConnectedPorts(connected);");
            sb.AppendLine("                    foreach (var other in connected)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        var toNode = other.GetNode();");
            sb.AppendLine("                        if (toNode == null || !registrations.TryGetValue(toNode, out var toReg)) continue;");
            sb.AppendLine("                        var toIsFlow = toReg.FlowInputPortNames.Contains(other.Name);");
            sb.AppendLine("                        var toIsData = toReg.DataInputPortNames.Contains(other.Name);");
            sb.AppendLine("                        if (fromIsFlow && toIsFlow) continue;");
            sb.AppendLine("                        if (fromIsData && toIsData) continue;");
            sb.AppendLine("                        logger.LogError($\"[EFG-V03] Edge {fromNode.GetType().Name}.{port.Name} → {toNode.GetType().Name}.{other.Name} pairs incompatible port kinds (flow↔data or unknown port).\", fromNode);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void ValidateFlowOutSingleConnection(Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration> registrations, GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            // GraphToolkit's UI doesn't expose a per-port connection-cap API, so a flow output");
            sb.AppendLine("            // can be dragged into multiple flow inputs. Our hydration overwrites");
            sb.AppendLine("            // FlowOutPort.Connection on each pass and the executor walks one path — silently");
            sb.AppendLine("            // dropping the others. Error here so the user fixes it before bake.");
            sb.AppendLine("            var connected = new List<IPort>();");
            sb.AppendLine("            foreach (var kv in registrations)");
            sb.AppendLine("            {");
            sb.AppendLine("                var node = kv.Key;");
            sb.AppendLine("                var reg = kv.Value;");
            sb.AppendLine("                foreach (var port in node.GetOutputPorts())");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (!reg.FlowOutputPortNames.Contains(port.Name)) continue;");
            sb.AppendLine("                    connected.Clear();");
            sb.AppendLine("                    port.GetConnectedPorts(connected);");
            sb.AppendLine("                    if (connected.Count > 1)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        logger.LogError($\"[EFG-V05] Flow output {node.GetType().Name}.{port.Name} has {connected.Count} connections — flow outputs must connect to exactly one flow input. The executor walks one path and drops the rest. Use a Branch node if you need conditional fan-out.\", node);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        static void ValidateRunFlowTerminates(Dictionary<INode, GraphPackageRegistry<" + p.RunnerTypeName + ">.NodeRegistration> registrations, GraphLogger logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Walk forward from every entry node along its flow outputs. A node is a flow");
            sb.AppendLine("            // terminator iff its registry entry has zero FlowOutputPortIds (Return/Cancel/");
            sb.AppendLine("            // ReturnBool, plus any future custom terminator). Warn when a flow output is");
            sb.AppendLine("            // unwired and the owning node is itself not a terminator — that path falls off");
            sb.AppendLine("            // the end of the graph instead of reaching an explicit Return/Cancel.");
            sb.AppendLine("            var connected = new List<IPort>();");
            sb.AppendLine("            var visited = new HashSet<INode>();");
            sb.AppendLine("            var queue = new Queue<INode>();");
            sb.AppendLine("            foreach (var kv in registrations)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (kv.Value.EntryTypeId == null) continue;");
            sb.AppendLine("                queue.Enqueue(kv.Key);");
            sb.AppendLine("                visited.Clear();");
            sb.AppendLine("                while (queue.Count > 0)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var node = queue.Dequeue();");
            sb.AppendLine("                    if (!visited.Add(node)) continue;");
            sb.AppendLine("                    if (!registrations.TryGetValue(node, out var reg)) continue;");
            sb.AppendLine("                    if (reg.FlowOutputPortNames.Count == 0) continue; // terminator");
            sb.AppendLine("                    foreach (var port in node.GetOutputPorts())");
            sb.AppendLine("                    {");
            sb.AppendLine("                        if (!reg.FlowOutputPortNames.Contains(port.Name)) continue;");
            sb.AppendLine("                        connected.Clear();");
            sb.AppendLine("                        port.GetConnectedPorts(connected);");
            sb.AppendLine("                        if (connected.Count == 0)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            logger.LogWarning($\"[EFG-V04] Flow path {node.GetType().Name}.{port.Name} ends without a Return/Cancel terminator — the executor will simply stop.\", node);");
            sb.AppendLine("                            continue;");
            sb.AppendLine("                        }");
            sb.AppendLine("                        foreach (var other in connected)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            var nextNode = other.GetNode();");
            sb.AppendLine("                            if (nextNode != null) queue.Enqueue(nextNode);");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        static string EditorGraphToolkitNamespace(Compilation compilation)
        {
            return EditorPackageRoot(compilation) + ".Editor.GToolkit";
        }

        static string EditorImporterNamespace(Compilation compilation)
        {
            return EditorPackageRoot(compilation) + ".Editor";
        }

        static string EditorPackageRoot(Compilation compilation)
        {
            var name = compilation.Assembly.Name ?? "";
            const string suffix = ".Editor";
            if (name.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }

            return name;
        }

        static void AppendEditorGraphFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            AppendEditorGraphHeader(sb, compilation, p);
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorGraphToolkitNamespace(compilation)}");
            sb.AppendLine("{");
            AppendEditorGraphBody(sb, p);
            sb.AppendLine("}");
        }

        static void AppendEditorGraphHeader(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            var pkgRoot = EditorPackageRoot(compilation);
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine($"using {pkgRoot};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine($"using {GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation)};");
            sb.AppendLine($"using {GraphRegistryEmitter.EditorRegistryNamespace};");
            sb.AppendLine("using Unity.GraphToolkit.Editor;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine($"using {GraphRegistryEmitter.PackageEditorGToolkitNamespace};");
        }

        static void AppendRunnerUsingIfNeeded(StringBuilder sb, GraphPackageModel p)
        {
            if (string.IsNullOrEmpty(p.RunnerNamespace))
            {
                return;
            }

            sb.AppendLine($"using {p.RunnerNamespace};");
        }

        static void AppendEditorGraphBody(StringBuilder sb, GraphPackageModel p)
        {
            var registryType = GraphRegistryEmitter.RegistryTypeName(p);
            sb.AppendLine("    [Serializable]");
            sb.AppendLine("    [Graph(AssetExtension)]");
            sb.AppendLine($"    public sealed partial class {p.GraphStem}Graph : Graph<{p.RunnerTypeName}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public override GraphPackageRegistry<{p.RunnerTypeName}> Registry => {registryType}.Instance;");
            sb.AppendLine();
            AppendEditorGraphConstantsAndMenu(sb, p);
            sb.AppendLine("    }");
        }

        static void AppendEditorGraphConstantsAndMenu(StringBuilder sb, GraphPackageModel p)
        {
            var extLit = EscapeString(p.Extension);
            var promptLit = EscapeString(CreatePromptTitle(p.AssetMenu));
            var menuLit = EscapeString(MenuItemPath(p.AssetMenu));
            sb.AppendLine($"        internal const string AssetExtension = \"{extLit}\";");
            sb.AppendLine($"        const string k_CreateMenuName = \"{promptLit}\";");
            sb.AppendLine($"        [MenuItem(\"{menuLit}\")]");
            sb.AppendLine($"        static void CreateAssetFile() =>");
            sb.AppendLine($"            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<{p.GraphStem}Graph>(k_CreateMenuName);");
        }

        static string MenuItemPath(string assetMenu)
        {
            return "Assets/Create/" + assetMenu;
        }

        static string CreatePromptTitle(string assetMenu)
        {
            return assetMenu.Replace("/", " ");
        }

        static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static void AppendEditorImporterFile(StringBuilder sb, Compilation compilation, GraphPackageModel p)
        {
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine($"using {EditorGraphToolkitNamespace(compilation)};");
            AppendRunnerUsingIfNeeded(sb, p);
            sb.AppendLine($"using {GraphRegistryEmitter.ResolveRegistryNamespace(p, compilation)};");
            sb.AppendLine($"using {GraphRegistryEmitter.EditorRegistryNamespace};");
            sb.AppendLine("using UnityEditor.AssetImporters;");
            sb.AppendLine();
            sb.AppendLine($"namespace {EditorImporterNamespace(compilation)}");
            sb.AppendLine("{");
            AppendImporterBody(sb, p);
            sb.AppendLine("}");
        }

        static void AppendImporterBody(StringBuilder sb, GraphPackageModel p)
        {
            sb.AppendLine($"    [ScriptedImporter(1, {p.GraphStem}Graph.AssetExtension)]");
            sb.AppendLine($"    public sealed class {p.GraphStem}GraphImporter : GraphAssetImporterBase<{p.GraphStem}Graph, {p.RunnerTypeName}, {p.GraphStem}GraphAsset>");
            sb.AppendLine("    {");
            sb.AppendLine($"        protected override GraphPackageRegistry<{p.RunnerTypeName}> Registry => {GraphRegistryEmitter.RegistryTypeName(p)}.Instance;");
            sb.AppendLine("    }");
        }
    }
}
