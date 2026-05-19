#nullable enable

namespace Scaffold.GraphFlow
{
    public sealed class FlowConnection
    {
        public FlowOutPort Source { get; }
        public FlowInPort Destination { get; }

        internal FlowConnection(FlowOutPort source, FlowInPort destination)
        {
            Source = source;
            Destination = destination;
        }
    }
}
