using System;

namespace Framework.TypeRouting.Tests
{
    public sealed class ByeReader : INoteReader
    {
        public Type RouteType => typeof(Bye);
    }
}
