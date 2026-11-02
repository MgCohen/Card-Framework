using System;
using Framework.RecordIds;
using VContainer;

namespace Framework.Blocks
{
    public static class BlocksInstall
    {
        // The caller registers EventStream, and IEventEmitter on the server.
        // BlockPositions subscribes at build, before the listeners that resolve later.
        public static IContainerBuilder AddBlocks(this IContainerBuilder builder, Action<BlocksOptions> configure = null)
        {
            var options = new BlocksOptions();
            configure?.Invoke(options);

            // Each system can call AddBlocks; the calls merge into one install.
            for (var i = 0; i < builder.Count; i++)
            {
                if (builder[i] is BlocksRegistration blocks)
                {
                    blocks.Match(options.Role);
                    return builder;
                }
            }

            if (builder.Exists(typeof(BlockPositions), includeInterfaceTypes: true, findParentScopes: true))
            {
                // A parent scope has Blocks; its Role is only readable from the built scope.
                builder.RegisterBuildCallback(scope => BlocksRegistration.InParents(scope)?.Match(options.Role));
                return builder;
            }

            builder.Register(new BlocksRegistration(options.Role));
            builder.RegisterBuildCallback(scope => scope.Resolve<BlockPositions>());
            if (options.Role == Role.Server) builder.Register<GroupService>(Lifetime.Singleton);
            return builder;
        }
    }
}
