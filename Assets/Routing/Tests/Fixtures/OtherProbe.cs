using System;

namespace Framework.Routing.Tests
{
    public sealed class OtherProbe : Probe
    {
        public OtherProbe(Type routeType, int order = 0) : base(routeType, order) { }
    }
}
