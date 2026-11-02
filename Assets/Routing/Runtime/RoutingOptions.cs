using System;
using System.Collections.Generic;
using VContainer;

namespace Framework.Routing
{
    public sealed class RoutingOptions
    {
        readonly List<Action<IContainerBuilder>> _routes = new();

        public bool Scan { get; set; } = true;
        public bool IncludeTestAssemblies { get; set; }

        public RoutingOptions Route<T>(RouteMode mode) where T : class, IRouted
        {
            // Reads Scan and IncludeTestAssemblies at Apply time, so their order in configure does not matter.
            _routes.Add(builder => builder.RegisterTypeRouter<T>(mode, Scan, IncludeTestAssemblies));
            return this;
        }

        internal void Apply(IContainerBuilder builder)
        {
            foreach (var route in _routes)
                route(builder);
        }
    }
}
