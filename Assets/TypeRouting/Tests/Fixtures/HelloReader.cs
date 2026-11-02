using System;

namespace Framework.TypeRouting.Tests
{
    public sealed class HelloReader : INoteReader
    {
        public Type RouteType => typeof(Hello);
    }
}
