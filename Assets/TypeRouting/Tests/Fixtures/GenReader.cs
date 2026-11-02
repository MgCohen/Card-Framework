using System;

namespace Framework.TypeRouting.Tests
{
    public sealed class GenReader : INoteReader
    {
        public Type RouteType => typeof(Gen<int>);
    }
}
