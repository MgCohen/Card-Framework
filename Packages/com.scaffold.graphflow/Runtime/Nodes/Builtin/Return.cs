using System;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed class Return<TResult> : RuntimeNode
    {
        public FlowInPort In = null!;
        public InputPort<TResult> Value = null!;

        public Return()
        {
            Value = new InputPort<TResult>();
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                SetResultOn(flow, Value.Read(flow));
                return flow.Return();
            });
            Ports.Add(In.Name, In);
            Ports.Add(nameof(Value), Value);
        }

        // Mirrors EntryRuntimeNode<TPayload>.PayloadOf — the cast succeeds when
        // the caller used Run<TEntry, TR> with TR equal to (or a base of) TResult,
        // and throws InvalidCastException for unrelated types. The `in` variance
        // on IFlowResult is what makes "return a child class" Just Work.
        static void SetResultOn(Flow flow, TResult value) =>
            ((IFlowResult<TResult>)flow).SetResult(value);
    }

    [Serializable]
    [GraphNode(Category = "Flow")]
    public sealed partial class Return : RuntimeNode
    {
        public FlowInPort In = null!;

        partial void InitializePorts() =>
            In = FlowInPort.Sync(this, nameof(In), flow => flow.Return());
    }
}
