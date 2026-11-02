using System;

namespace Framework.TypeRouting.Tests
{
    public sealed class OpenReader<T> : INoteReader
    {
        public Type RouteType => typeof(Gen<T>);
    }
}
