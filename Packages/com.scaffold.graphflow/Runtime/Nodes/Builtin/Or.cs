using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Logic")]
    public sealed partial class Or : RuntimeNode
    {
        public InputPort<bool> A = null!;
        public InputPort<bool> B = null!;
        public OutputPort<bool> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<bool>(flow => A.Read(flow) || B.Read(flow));
    }
}
