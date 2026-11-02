using System;
using System.Reflection;
using Framework.RecordIds;
using Framework.TypeRouting;
using Framework.Wire;
using VContainer;

namespace Framework.Blocks
{
    public static class BlocksInstall
    {
        static readonly MethodInfo NetworkRegister = typeof(Network).GetMethod(nameof(Network.Register));

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

        // Channel ids are positional. TypeScan sorts by full name, so both ends register in the same order.
        static void RegisterOn(Network network, bool includeTests)
        {
            foreach (var type in TypeScan.ConcreteTypesOf(typeof(Block), includeTests))
            {
                var packed = type.GetNestedType("Packed")
                    ?? throw new InvalidOperationException(
                        $"Block '{type.FullName}' has no nested Packed type. Make it partial with a [Packed] field.");
                NetworkRegister.MakeGenericMethod(type, packed).Invoke(network, null);
            }
        }
    }
}
