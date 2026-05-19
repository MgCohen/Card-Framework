#nullable enable
using UnityEngine;

namespace Scaffold.GraphFlow
{
    public abstract class GraphBuilder<TRunner> where TRunner : GraphRunner
    {
        public TRunner Build(GraphAsset<TRunner> asset)
        {
            var baked = GraphTopology.Bake(asset);
            var runner = CreateRunner(baked);
            runner.Variables = runner.CreateVariableBag(baked.Variables);
            WireVariableEdges(baked, runner);
            BakePortCaches(baked, runner.MaxConcurrentFlows);
            foreach (var n in baked.Nodes) n.Initialize(runner);
            runner.Initialize();
            return runner;
        }

        // Sizes per-port caches to the runner's flow-index budget so the
        // read hot path can index without bounds checks of its own.
        static void BakePortCaches(BakedGraph baked, int maxFlows)
        {
            foreach (var node in baked.Nodes)
            {
                foreach (var kvp in node.Ports)
                    kvp.Value.Bake(maxFlows);
            }
        }

        protected abstract TRunner CreateRunner(BakedGraph baked);

        // Wires each VariableEdge by setting the destination input port's source to a
        // closure that reads the runner's typed variable handle. Note: BakedGraph (and the
        // RuntimeNode instances inside it) is cached per-asset on the builder, so building
        // the same asset twice with different runners would have the second wire-pass
        // overwrite the first. Same constraint already applies to Get/Set node Initialize
        // caching — assumes one-runner-per-asset within a builder lifetime.
        static void WireVariableEdges(BakedGraph baked, GraphRunner runner)
        {
            foreach (var ve in baked.VariableEdges)
            {
                if (string.IsNullOrEmpty(ve.variableId)) continue;
                if (!baked.NodesById.TryGetValue(ve.toNodeId, out var node)) continue;
                if (!node.Ports.TryGetValue(ve.toPortName, out var port)) continue;
                if (!runner.Variables.TryGet(ve.variableId, out var handle))
                {
                    Debug.LogWarning($"GraphFlow: variable '{ve.variableId}' not declared; port {node.GetType().Name}.{ve.toPortName} left unconnected.");
                    continue;
                }
                port.ConnectFromVariable(handle);
            }
        }
    }
}
