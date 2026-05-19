using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Loop : RuntimeNode
    {
        public InputPort<int> Count = null!;
        public FlowInPort Begin = null!;
        public FlowInPort Continue = null!;
        public FlowOutPort Body = null!;
        public FlowOutPort Done = null!;
        public OutputPort<int> Iteration = null!;

        int[] _iterations = Array.Empty<int>();

        public override void Initialize(GraphRunner runner) =>
            _iterations = new int[runner.MaxConcurrentFlows];

        partial void InitializePorts()
        {
            Iteration = new OutputPort<int>(flow => _iterations[flow.Index], cache: false);

            Begin = FlowInPort.Sync(this, nameof(Begin), flow =>
            {
                _iterations[flow.Index] = 0;
                return Count.Read(flow) > 0 ? Body : Done;
            });

            Continue = FlowInPort.Sync(this, nameof(Continue), flow =>
            {
                flow.InvalidateAll();
                var i = ++_iterations[flow.Index];
                return i < Count.Read(flow) ? Body : Done;
            });
        }
    }
}
