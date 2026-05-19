#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class BakedGraph
    {
        public IReadOnlyList<RuntimeNode> Nodes { get; }
        public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }
        public IReadOnlyList<RuntimeVariable> Variables { get; }
        public IReadOnlyList<VariableEdge> VariableEdges { get; }
        internal IReadOnlyDictionary<int, RuntimeNode> NodesById { get; }

        internal BakedGraph(
            IReadOnlyList<RuntimeNode> nodes,
            IReadOnlyDictionary<Type, EntryRuntimeNodeBase> entries,
            IReadOnlyList<RuntimeVariable> variables,
            IReadOnlyList<VariableEdge> variableEdges,
            IReadOnlyDictionary<int, RuntimeNode> nodesById)
        {
            Nodes = nodes;
            EntriesByPayload = entries;
            Variables = variables;
            VariableEdges = variableEdges;
            NodesById = nodesById;
        }
    }
}
