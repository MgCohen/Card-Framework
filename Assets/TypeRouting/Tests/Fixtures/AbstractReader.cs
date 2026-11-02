using System;

namespace Framework.TypeRouting.Tests
{
    public abstract class AbstractReader : INoteReader
    {
        public Type RouteType => typeof(Hello);
    }
}
