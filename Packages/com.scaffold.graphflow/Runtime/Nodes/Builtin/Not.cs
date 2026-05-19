using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Logic")]
    public sealed partial class Not : RuntimeNode
    {
        public InputPort<bool> Value = null!;
        public OutputPort<bool> Result = null!;

        partial void InitializePorts() =>
            Result = new OutputPort<bool>(flow => !Value.Read(flow));
    }
}
