using System;

namespace Framework.Routing.Tests
{
    public sealed class ByeReader : INoteReader
    {
        public Type RouteType => typeof(Bye);
    }
}
