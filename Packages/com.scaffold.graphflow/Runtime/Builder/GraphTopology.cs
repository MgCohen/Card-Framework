#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    internal static class GraphTopology
    {
        public static BakedGraph Bake(GraphAsset asset)
        {
            var nodes = asset.nodes;
            var byId = new Dictionary<int, RuntimeNode>(nodes.Count);
            foreach (var n in nodes)
            {
                if (n != null) byId[n.nodeId] = n;
            }

            var dataByDest = new Dictionary<int, List<DataBinding>>();
            var flowByDest = new Dictionary<int, List<FlowBinding>>();

            foreach (var c in asset.connections)
            {
                if (!byId.TryGetValue(c.fromNodeId, out var from)) continue;
                if (!byId.TryGetValue(c.toNodeId, out var to)) continue;
                if (!from.Ports.TryGetValue(c.fromPortName, out var srcPort)) continue;
                if (!to.Ports.TryGetValue(c.toPortName, out var dstPort)) continue;

                if (!dataByDest.TryGetValue(c.toNodeId, out var list))
                    dataByDest[c.toNodeId] = list = new List<DataBinding>();
                list.Add(new DataBinding(srcPort, dstPort));
            }

            foreach (var e in asset.flowEdges)
            {
                if (!byId.TryGetValue(e.fromNodeId, out var from)) continue;
                if (!byId.TryGetValue(e.toNodeId, out var to)) continue;
                if (!from.Ports.TryGetValue(e.fromPortName, out var srcPort) || srcPort is not FlowOutPort flowOut) continue;
                if (!to.Ports.TryGetValue(e.toPortName, out var dstPort) || dstPort is not FlowInPort flowIn) continue;

                var connection = new FlowConnection(flowOut, flowIn);
                var binding = new FlowBinding(flowOut, flowIn, connection);

                if (!flowByDest.TryGetValue(e.toNodeId, out var list))
                    flowByDest[e.toNodeId] = list = new List<FlowBinding>();
                list.Add(binding);
            }

            var emptyData = Array.Empty<DataBinding>();
            var emptyFlow = Array.Empty<FlowBinding>();

            foreach (var n in nodes)
            {
                if (n == null) continue;
                IReadOnlyList<DataBinding> data = dataByDest.TryGetValue(n.nodeId, out var d) ? d : emptyData;
                IReadOnlyList<FlowBinding> flow = flowByDest.TryGetValue(n.nodeId, out var f) ? f : emptyFlow;
                n.Build(new NodeBuildSlice(data, flow));
            }

            var entries = new Dictionary<Type, EntryRuntimeNodeBase>();
            foreach (var n in nodes)
            {
                if (n is EntryRuntimeNodeBase entry)
                    entries[entry.PayloadType] = entry;
            }

            var variables     = asset.variables     ?? (IReadOnlyList<RuntimeVariable>)Array.Empty<RuntimeVariable>();
            var variableEdges = asset.variableEdges ?? (IReadOnlyList<VariableEdge>)Array.Empty<VariableEdge>();
            return new BakedGraph(nodes, entries, variables, variableEdges, byId);
        }
    }
}
