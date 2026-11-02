using System;

namespace Framework.Routing.Tests
{
    public abstract class AbstractReader : INoteReader
    {
        public Type RouteType => typeof(Hello);
    }
}
