using System;

namespace Framework.Routing.Tests
{
    public sealed class HelloReader : INoteReader
    {
        public Type RouteType => typeof(Hello);
    }
}
