#nullable enable
using System.Collections.Generic;
using Scaffold.GraphFlow.Nodes;

namespace Scaffold.GraphFlow.Editor
{
    // Edit-time validator for graph asset Return<TResult> consistency.
    // Walks each entry's reachable flow graph, collects all Return<X> nodes,
    // and flags entries whose reachable Return types are mutually
    // unrelated — no caller of Run<*, TResult> could satisfy both paths,
    // so at least one branch is guaranteed to throw InvalidCastException
    // at runtime.
    //
    // What this catches:
    //   Entry → Return<Spell>  AND  Entry → Return<Ball>   (no common base)
    //
    // What this allows:
    //   Entry → Return<FireSpell> AND Entry → Return<IceSpell>  (both share Spell)
    //
    // What it doesn't catch (handled by runtime cast in Return<TResult>):
    //   Caller used wrong TResult or no TResult on a graph with Return<X>.
    //   That requires C# call-site analysis, which is a separate concern.
    public static class GraphResultValidator
    {
        public readonly struct Diagnostic
        {
            public string Message { get; }
            public int EntryNodeId { get; }
            public Diagnostic(string message, int entryNodeId)
            {
                Message = message;
                EntryNodeId = entryNodeId;
            }
        }

        public static IReadOnlyList<Diagnostic> Validate(GraphAsset asset)
        {
            var diagnostics = new List<Diagnostic>();
            if (asset == null || asset.nodes == null) return diagnostics;

            var nodesById = new Dictionary<int, RuntimeNode>(asset.nodes.Count);
            foreach (var n in asset.nodes)
                if (n != null) nodesById[n.nodeId] = n;

            var flowsByFrom = new Dictionary<int, List<Edge>>();
            foreach (var e in asset.flowEdges)
            {
                if (!flowsByFrom.TryGetValue(e.fromNodeId, out var list))
                    flowsByFrom[e.fromNodeId] = list = new List<Edge>();
                list.Add(e);
            }

            foreach (var node in asset.nodes)
            {
                if (node is not EntryRuntimeNodeBase entry) continue;
                ValidateEntry(asset, entry, nodesById, flowsByFrom, diagnostics);
            }

            return diagnostics;
        }

        static void ValidateEntry(
            GraphAsset asset,
            EntryRuntimeNodeBase entry,
            Dictionary<int, RuntimeNode> nodesById,
            Dictionary<int, List<Edge>> flowsByFrom,
            List<Diagnostic> diagnostics)
        {
            var returns = CollectReachableReturns(entry, nodesById, flowsByFrom);
            if (returns.Count < 2) return;

            // Pairwise: every pair must be related (one assignable from other).
            // Equivalent to "all share a common base above object" — we only
            // flag pairs that are completely unrelated.
            for (int i = 0; i < returns.Count; i++)
            {
                var a = returns[i];
                for (int j = i + 1; j < returns.Count; j++)
                {
                    var b = returns[j];
                    if (a.ResultType.IsAssignableFrom(b.ResultType)) continue;
                    if (b.ResultType.IsAssignableFrom(a.ResultType)) continue;

                    diagnostics.Add(new Diagnostic(
                        $"[GraphFlow] '{asset.name}': entry '{entry.GetType().Name}' (node {entry.nodeId}) reaches " +
                        $"Return<{a.ResultType.Name}> (node {a.NodeId}) and Return<{b.ResultType.Name}> (node {b.NodeId}) " +
                        $"with no common base. Run<*, TResult> can satisfy at most one — the other path will throw at runtime.",
                        entry.nodeId));
                }
            }
        }

        readonly struct ReachableReturn
        {
            public int NodeId { get; }
            public System.Type ResultType { get; }
            public ReachableReturn(int nodeId, System.Type resultType)
            {
                NodeId = nodeId;
                ResultType = resultType;
            }
        }

        static List<ReachableReturn> CollectReachableReturns(
            EntryRuntimeNodeBase entry,
            Dictionary<int, RuntimeNode> nodesById,
            Dictionary<int, List<Edge>> flowsByFrom)
        {
            var found = new List<ReachableReturn>();
            var visited = new HashSet<int> { entry.nodeId };
            var queue = new Queue<int>();
            queue.Enqueue(entry.nodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!flowsByFrom.TryGetValue(nodeId, out var edges)) continue;
                foreach (var edge in edges)
                {
                    if (!visited.Add(edge.toNodeId)) continue;
                    if (!nodesById.TryGetValue(edge.toNodeId, out var next)) continue;
                    var resultType = GetReturnResultType(next);
                    if (resultType != null)
                        found.Add(new ReachableReturn(next.nodeId, resultType));
                    queue.Enqueue(edge.toNodeId);
                }
            }

            return found;
        }

        // Returns the TResult generic argument for a Return<TResult> node,
        // null for anything else. Extension point: future return-shape nodes
        // (custom user-defined return writers) would need to be recognized
        // here, e.g. via a marker interface like IGraphReturnNode<TResult>.
        static System.Type? GetReturnResultType(RuntimeNode node)
        {
            var t = node.GetType();
            if (!t.IsGenericType) return null;
            if (t.GetGenericTypeDefinition() != typeof(Return<>)) return null;
            return t.GetGenericArguments()[0];
        }
    }
}
