#nullable enable
using Cysharp.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    public abstract class CardCommandDispatcher<TCmd, TResult> : RuntimeNode<CardEffectRunner>
        where TCmd : Command<TResult>, new()
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort FlowOut = null!;

        protected CardCommandDispatcher()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            FlowIn = FlowInPort.Async(this, nameof(FlowIn), async (Flow flow) =>
            {
                var cmd = BuildPayload(flow);
                var result = await cmd.Execute(Runner(flow), flow);
                WriteOutputs(flow, result);
                return (FlowOutPort?)FlowOut;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
        }

        protected abstract TCmd BuildPayload(Flow flow);
        protected abstract void WriteOutputs(Flow flow, TResult result);
    }
}
