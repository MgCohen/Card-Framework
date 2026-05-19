using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Convert")]
    public sealed partial class IntToString : RuntimeNode
    {
        public InputPort<int> Value = null!;
        public OutputPort<string> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<string>(flow => Value.Read(flow).ToString());
    }
}
