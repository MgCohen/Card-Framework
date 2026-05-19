using System;
using System.Collections.Generic;
using System.Linq;
using Scaffold.GraphFlow.Editor.GToolkit;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    public sealed class GraphBakeResult<TAsset> where TAsset : ScriptableObject
    {
        public TAsset? Asset { get; set; }
        public bool HasErrors { get; set; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;
        readonly List<string> _diagnostics = new();

        public void LogError(string msg)
        {
            _diagnostics.Add(msg);
            HasErrors = true;
        }
    }

    /// <summary>
    /// Generic, registry-driven baker. One implementation translates any GraphFlow package's editor graph
    /// into its typed runtime asset — the generator emits <c>&lt;Stem&gt;GraphRegistry</c>, which supplies
    /// per-editor-node factories + port-name lookups so this code stays free of per-payload switches.
    /// </summary>
    public static class GraphBakerCore
    {
        public const int SchemaVersion = 4;

        public static GraphBakeResult<TAsset> Bake<TRunner, TAsset>(
            Graph<TRunner> editorGraph,
            TAsset? previousRuntime,
            GraphPackageRegistry<TRunner> registry)
            where TRunner : GraphRunner
            where TAsset : GraphAsset<TRunner>
        {
            var result = new GraphBakeResult<TAsset>();

            if (editorGraph == null)
            {
                result.LogError("Editor graph is null.");
                return result;
            }

            var editorNodes = editorGraph.GetNodes().ToList();
            if (editorNodes.Count == 0)
                return result;

            var guidToNodeId = RecoverGuidMap<TRunner, TAsset>(previousRuntime);
            var nextId = NextFreeNodeId(guidToNodeId);

            var editorToRuntime = new Dictionary<INode, RuntimeNode>();
            var editorToRegistration = new Dictionary<INode, GraphPackageRegistry<TRunner>.NodeRegistration>();

            foreach (var n in editorNodes)
            {
                if (n is IConstantNode || n is IVariableNode)
                    continue;

                var reg = registry.Lookup(n.GetType());
                if (reg == null)
                {
                    result.LogError($"Unsupported editor node: {n.GetType().FullName}");
                    return result;
                }

                var guid = EditorNodeIdentity.GetStableGuid(n);

                if (!guidToNodeId.TryGetValue(guid, out var nodeId))
                {
                    nodeId = nextId++;
                    guidToNodeId[guid] = nodeId;
                }

                var runtime = reg.Factory(n);
                runtime.nodeId = nodeId;
                runtime.editorGuid = guid;
                editorToRuntime[n] = runtime;
                editorToRegistration[n] = reg;
            }

            var dataConnections = new List<Edge>();
            var flowEdges = new List<Edge>();

            foreach (var pair in editorToRuntime.OrderBy(p => p.Value.nodeId))
            {
                var editorNode = pair.Key;
                var fromRuntime = pair.Value;
                var fromReg = editorToRegistration[editorNode];

                foreach (var port in editorNode.GetOutputPorts())
                {
                    var connected = new List<IPort>();
                    port.GetConnectedPorts(connected);
                    foreach (var other in connected)
                    {
                        var toEditor = other.GetNode();
                        if (toEditor == null || !editorToRuntime.TryGetValue(toEditor, out var toRuntime))
                            continue;

                        var toReg = editorToRegistration[toEditor];
                        if (TryRecordFlowEdge(fromReg, port.Name, fromRuntime.nodeId, toReg, other.Name, toRuntime.nodeId, flowEdges))
                            continue;

                        if (TryRecordDataEdge(fromReg, port.Name, fromRuntime.nodeId, toReg, other.Name, toRuntime.nodeId, dataConnections))
                            continue;

                        result.LogError($"Edge from {editorNode.GetType().Name}.{port.Name} to {toEditor.GetType().Name}.{other.Name} matches neither flow nor data ports.");
                        return result;
                    }
                }
            }

            var (variables, variableEdges) = BakeVariables(editorGraph, editorNodes, editorToRuntime, editorToRegistration, result);
            if (result.HasErrors) return result;

            var asset = ScriptableObject.CreateInstance<TAsset>();
            asset.nodes = editorToRuntime.Values.OrderBy(n => n.nodeId).ToList();
            asset.connections = SortEdges(dataConnections);
            asset.flowEdges = SortEdges(flowEdges);
            asset.variables = variables;
            asset.variableEdges = variableEdges;
            asset.schemaVersion = SchemaVersion;

            result.Asset = asset;
            return result;
        }

        static bool TryRecordFlowEdge<TRunner>(
            GraphPackageRegistry<TRunner>.NodeRegistration fromReg, string fromPortName, int fromNodeId,
            GraphPackageRegistry<TRunner>.NodeRegistration toReg, string toPortName, int toNodeId,
            List<Edge> into) where TRunner : GraphRunner
        {
            if (!fromReg.FlowOutputPortNames.Contains(fromPortName))
                return false;
            if (!toReg.FlowInputPortNames.Contains(toPortName))
                return false;
            into.Add(new Edge { fromNodeId = fromNodeId, fromPortName = fromPortName, toNodeId = toNodeId, toPortName = toPortName });
            return true;
        }

        static bool TryRecordDataEdge<TRunner>(
            GraphPackageRegistry<TRunner>.NodeRegistration fromReg, string fromPortName, int fromNodeId,
            GraphPackageRegistry<TRunner>.NodeRegistration toReg, string toPortName, int toNodeId,
            List<Edge> into) where TRunner : GraphRunner
        {
            if (!fromReg.DataOutputPortNames.Contains(fromPortName))
                return false;
            if (!toReg.DataInputPortNames.Contains(toPortName))
                return false;
            into.Add(new Edge { fromNodeId = fromNodeId, fromPortName = fromPortName, toNodeId = toNodeId, toPortName = toPortName });
            return true;
        }

        static (List<RuntimeVariable> variables, List<VariableEdge> variableEdges)
            BakeVariables<TRunner, TAsset>(
                Graph<TRunner> editorGraph,
                List<INode> editorNodes,
                Dictionary<INode, RuntimeNode> editorToRuntime,
                Dictionary<INode, GraphPackageRegistry<TRunner>.NodeRegistration> editorToRegistration,
                GraphBakeResult<TAsset> result)
            where TRunner : GraphRunner
            where TAsset : GraphAsset<TRunner>
        {
            var variables = new List<RuntimeVariable>();
            var variableEdges = new List<VariableEdge>();

            // 1. Declared variables from the GT blackboard panel.
            var idByVariable = new Dictionary<IVariable, string>();
            foreach (var v in editorGraph.GetVariables())
            {
                if (v == null) continue;
                var id = EditorVariableIdentity.GetStableGuid(v);
                if (string.IsNullOrEmpty(id))
                {
                    result.LogError($"Variable {v.Name ?? "<unnamed>"} has no stable identity — cannot bake.");
                    return (variables, variableEdges);
                }
                var def = EditorBlackboardVariables.CreateFor(v);
                if (def == null)
                {
                    result.LogError($"Variable {v.Name} has unsupported DataType {v.DataType?.FullName ?? "<null>"}.");
                    return (variables, variableEdges);
                }
                var varType = v.DataType!;
                if (varType.AssemblyQualifiedName != def.ValueType.AssemblyQualifiedName)
                {
                    result.LogError($"Variable '{v.Name}': dataType ({varType.FullName}) does not match default value type ({def.ValueType.FullName}).");
                    return (variables, variableEdges);
                }
                variables.Add(new RuntimeVariable
                {
                    id = id!,
                    name = v.Name ?? string.Empty,
                    typeName = varType.AssemblyQualifiedName,
                    defaultValue = def,
                });
                idByVariable[v] = id!;
            }

            // 2. Edges sourced from IVariableNode → runtime data input ports.
            //    Variable nodes are not registered runtime nodes (they were skipped
            //    in the main loop). They're sources only; the destination must be a
            //    declared data input on a runtime node.
            foreach (var n in editorNodes)
            {
                if (n is not IVariableNode vn) continue;
                if (vn.Variable == null) continue;
                if (!idByVariable.TryGetValue(vn.Variable, out var variableId))
                {
                    // Orphaned variable node — references a blackboard variable that was
                    // deleted or otherwise didn't make it into editorGraph.GetVariables().
                    // Surface as a warning so the designer notices, then skip.
                    UnityEngine.Debug.LogWarning(
                        $"GraphFlow bake: variable node {n.GetType().Name} references unknown variable '{vn.Variable.Name}'; edge skipped.");
                    continue;
                }

                var varType = vn.Variable.DataType;
                foreach (var port in n.GetOutputPorts())
                {
                    var connected = new List<IPort>();
                    port.GetConnectedPorts(connected);
                    foreach (var other in connected)
                    {
                        var toEditor = other.GetNode();
                        if (toEditor == null || !editorToRuntime.TryGetValue(toEditor, out var toRuntime)) continue;
                        var toReg = editorToRegistration[toEditor];
                        if (!toReg.DataInputPortNames.Contains(other.Name))
                        {
                            result.LogError($"Variable edge from {n.GetType().Name} to {toEditor.GetType().Name}.{other.Name}: destination is not a declared data input.");
                            return (variables, variableEdges);
                        }

                        var portType = other.DataType;
                        if (varType != null && portType != null && !portType.IsAssignableFrom(varType))
                        {
                            result.LogError(
                                $"Variable '{vn.Variable.Name}' ({varType.Name}) is incompatible with port {toEditor.GetType().Name}.{other.Name} ({portType.Name}).");
                            return (variables, variableEdges);
                        }

                        variableEdges.Add(new VariableEdge
                        {
                            variableId = variableId,
                            toNodeId = toRuntime.nodeId,
                            toPortName = other.Name,
                        });
                    }
                }
            }

            return (variables, variableEdges);
        }

        static Dictionary<string, int> RecoverGuidMap<TRunner, TAsset>(TAsset? previous)
            where TRunner : GraphRunner
            where TAsset : GraphAsset<TRunner>
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (previous == null)
                return map;

            foreach (var n in previous.nodes)
            {
                if (!string.IsNullOrEmpty(n.editorGuid))
                    map[n.editorGuid] = n.nodeId;
            }

            return map;
        }

        static int NextFreeNodeId(Dictionary<string, int> guidToNodeId)
        {
            var max = 0;
            foreach (var id in guidToNodeId.Values)
                if (id > max)
                    max = id;
            return max + 1;
        }

        static List<Edge> SortEdges(List<Edge> edges) => edges
            .OrderBy(e => e.fromNodeId)
            .ThenBy(e => e.fromPortName, StringComparer.Ordinal)
            .ThenBy(e => e.toNodeId)
            .ThenBy(e => e.toPortName, StringComparer.Ordinal)
            .ToList();
    }
}
