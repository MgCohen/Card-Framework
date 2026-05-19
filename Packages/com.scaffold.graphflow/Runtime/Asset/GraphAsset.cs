using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public struct Edge
    {
        public int fromNodeId;
        public string fromPortName;
        public int toNodeId;
        public string toPortName;
    }

    public abstract class GraphAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeReference] public List<RuntimeNode> nodes = new();
        public List<Edge> connections = new();
        public List<Edge> flowEdges = new();
        public List<RuntimeVariable> variables = new();
        public List<VariableEdge> variableEdges = new();
        public int schemaVersion;

        // Strip null SerializeReference entries on load — class renames in baked assets
        // leave dangling managed refs that NaughtyAttributes' inspector logs on every tick.
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (nodes != null) nodes.RemoveAll(n => n == null);
            else nodes = new List<RuntimeNode>();
        }
    }

    public abstract class GraphAsset<TRunner> : GraphAsset where TRunner : GraphRunner
    {
    }
}
