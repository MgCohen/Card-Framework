using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    public sealed class GraphPackageRegistry<TRunner> where TRunner : GraphRunner
    {
        public delegate RuntimeNode NodeFactory(INode editorNode);

        public sealed class NodeRegistration
        {
            public Type EditorNodeType = null!;
            public NodeFactory Factory = null!;
            public HashSet<string> DataInputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> DataOutputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> FlowInputPortNames = new(StringComparer.Ordinal);
            public HashSet<string> FlowOutputPortNames = new(StringComparer.Ordinal);
            public string? EntryTypeId;
        }

        readonly Dictionary<Type, NodeRegistration> _byEditorType = new();

        public void Register(NodeRegistration r) => _byEditorType[r.EditorNodeType] = r;

        public NodeRegistration? Lookup(Type editorType) =>
            _byEditorType.TryGetValue(editorType, out var v) ? v : null;
    }
}
