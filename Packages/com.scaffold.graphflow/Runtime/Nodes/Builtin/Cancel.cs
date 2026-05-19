using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Cancel : RuntimeNode
    {
        public FlowInPort In = null!;

        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow => flow.Cancel());
    }
}
