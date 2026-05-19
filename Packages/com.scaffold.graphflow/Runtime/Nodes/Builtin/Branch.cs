using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Branch : RuntimeNode
    {
        public InputPort<bool> Condition = null!;
        public FlowInPort In = null!;
        public FlowOutPort True = null!;
        public FlowOutPort False = null!;

        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In),
                flow => Condition.Read(flow) ? True : False);
    }
}
