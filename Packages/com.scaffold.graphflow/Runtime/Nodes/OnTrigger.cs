#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public sealed class OnTrigger<TEvent> : EntryRuntimeNode<TEvent>, IOnTrigger
        where TEvent : class
    {
        public Timing Timing { get; set; }

        public FlowOutPort FlowOut = null!;
        public OutputPort<TEvent> Event = null!;

        public OnTrigger()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Event = new OutputPort<TEvent>(flow => PayloadOf(flow));
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Event), Event);
        }
    }
}
