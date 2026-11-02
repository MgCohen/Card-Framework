using System;

namespace Framework.Routing.Tests
{
    public sealed class OpenReader<T> : INoteReader
    {
        public Type RouteType => typeof(Gen<T>);
    }
}
