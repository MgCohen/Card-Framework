using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Math")]
    public sealed partial class Add : RuntimeNode
    {
        public InputPort<int> A = null!;
        public InputPort<int> B = null!;
        public OutputPort<int> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<int>(flow => A.Read(flow) + B.Read(flow));
    }
}
