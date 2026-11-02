using System;

namespace Framework.Routing.Tests
{
    public sealed class GenReader : INoteReader
    {
        public Type RouteType => typeof(Gen<int>);
    }
}
