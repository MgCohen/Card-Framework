using System;

namespace Framework.Routing
{
    // Without this attribute the linker removes members that only the scan reaches.
    [RequireImplementors]
    public interface IRouted
    {
        Type RouteType { get; }
    }
}
