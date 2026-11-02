using System;
using System.Collections.Generic;
using VContainer;

namespace Framework.TypeRouting
{
    public sealed class TypeRoutingOptions
    {
        readonly List<Action<IContainerBuilder>> _routes = new();

        public bool Scan { get; set; } = true;
        public bool IncludeTestAssemblies { get; set; }

        public TypeRoutingOptions Route<T>(RouteMode mode) where T : class, IRouted
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
