#nullable enable

namespace Scaffold.GraphFlow
{
    public sealed class FlowOutPort : Port
    {
        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }

        public FlowOutPort(RuntimeNode owner, string name)
        {
            Owner = owner;
            Name = name;
        }
    }
}
