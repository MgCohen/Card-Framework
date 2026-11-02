using System;
using VContainer;

namespace Framework.Routing
{
    public static class RoutingInstall
    {
        public static IContainerBuilder AddRouting(this IContainerBuilder builder, Action<RoutingOptions> configure)
        {
            var options = new RoutingOptions();
            configure(options);
            options.Apply(builder);
            return builder;
        }

        public static void RegisterTypeRouter<T>(this IContainerBuilder builder, RouteMode mode,
            bool scan = true, bool includeTests = false) where T : class, IRouted
        {
            if (scan)
                foreach (var type in TypeScan.ConcreteTypesOf(typeof(T), includeTests))
                    builder.Register(type, Lifetime.Singleton).As<T>();
            builder.Register<TypeRouter<T>>(Lifetime.Singleton).WithParameter(mode);
        }
    }
}
