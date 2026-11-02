using System;

namespace Framework.TypeRouting.Tests
{
    // Not an INoteReader: the scan tests of INoteReader must not find it, because it has no default constructor.
    public class Probe : IRouted, IOrdered
    {
        public Probe(Type routeType, int order = 0)
        {
            RouteType = routeType;
            Order = order;
        }

        public Type RouteType { get; }
        public int Order { get; }
    }
}
