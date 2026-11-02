using System;
using Framework.RecordIds;
using Framework.TypeRouting;
using Framework.Wire;
using VContainer;

namespace Framework.Blocks
{
    public static class BlocksInstall
    {
        // The caller registers EventStream, and IEventEmitter on the server. If the caller also registers
        // Network, AddBlocks registers each concrete Block type on it.
        public static IContainerBuilder AddBlocks(this IContainerBuilder builder, Action<BlocksOptions> configure = null)
        {
            var options = new BlocksOptions();
            configure?.Invoke(options);

            // Each system can call AddBlocks; the calls merge into one install.
            for (var i = 0; i < builder.Count; i++)
            {
                if (builder[i] is BlocksRegistration blocks)
                {
                    blocks.Match(options);
                    return builder;
                }
            }

            if (builder.Exists(typeof(BlockPositions), includeInterfaceTypes: true, findParentScopes: true))
            {
                // A parent scope has Blocks; its options are only readable from the built scope.
                builder.RegisterBuildCallback(scope => BlocksRegistration.InParents(scope)?.Match(options));
                return builder;
            }

            builder.Register(new BlocksRegistration(options));
            builder.RegisterBuildCallback(scope => scope.Resolve<BlockPositions>());
            builder.RegisterBuildCallback(scope =>
            {
                if (scope.TryResolve<Network>(out var network)) RegisterOn(network, options.IncludeTests);
            });
            if (options.Role == Role.Server) builder.Register<GroupService>(Lifetime.Singleton);
            return builder;
        }

        // Warning: channel ids follow the registration order, so both ends must run the same installs in the
        // same order. AddBlocks on both ends registers the same block types: TypeScan sorts by full name, and
        // [RequireDerived] on Block keeps each block type in a stripped player. TryRegister skips a type that
        // WireRegistrar already registered.
        static void RegisterOn(Network network, bool includeTests)
        {
            foreach (var type in TypeScan.ConcreteTypesOf(typeof(Block), includeTests))
                network.TryRegister(type);
        }
    }
}
