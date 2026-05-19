#nullable enable
using System;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class EntryRuntimeNodeBase : RuntimeNode
    {
        public abstract Type PayloadType { get; }

        FlowOutPort _defaultOut = null!;
        public FlowOutPort GetDefaultOut() => _defaultOut;

        internal override void Build(in NodeBuildSlice slice)
        {
            base.Build(slice);

            FlowOutPort? found = null;
            int count = 0;
            foreach (var p in Ports.Values)
            {
                if (p is not FlowOutPort fo) continue;
                count++;
                found = fo;
            }

            if (count == 0)
                throw new InvalidOperationException(
                    $"Entry {GetType().Name} has no FlowOutPort — cannot dispatch via Run<TEntry>.");
            if (count > 1)
                throw new InvalidOperationException(
                    $"Entry {GetType().Name} has {count} FlowOutPorts — use RunFromFlowOut to pick one.");

            _defaultOut = found!;
        }
    }

    [Serializable]
    public abstract class EntryRuntimeNode<TPayload> : EntryRuntimeNodeBase
        where TPayload : class
    {
        public override Type PayloadType => typeof(TPayload);
        public OutputPort<TPayload> Payload { get; }

        protected EntryRuntimeNode()
        {
            Payload = new OutputPort<TPayload>(flow => PayloadOf(flow));
            Ports.Add(nameof(Payload), Payload);
        }

        // Typed payload accessor for entry-side node code. Cast is safe because
        // the runner always constructs Flow<TPayload> matching the entry's type.
        protected static TPayload PayloadOf(Flow flow) => ((Flow<TPayload>)flow).Payload;
    }
}
