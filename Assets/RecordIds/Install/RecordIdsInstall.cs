using System;
using VContainer;

namespace Framework.RecordIds
{
    public static class RecordIdsInstall
    {
        // Only the server gives ids; the client reads them from the wire.
        public static IContainerBuilder AddIdSpace<TId>(this IContainerBuilder builder, Role role, int first = 1)
            where TId : Id, new()
        {
            if (first < 1) throw new ArgumentOutOfRangeException(nameof(first), first, "The first id must be 1 or more.");
            if (role != Role.Server) return builder;

            // Each system declares the id spaces it uses, so two systems can declare the same one.
            for (var i = 0; i < builder.Count; i++)
            {
                if (builder[i] is IdSpaceRegistration<TId> space)
                {
                    space.Match(first);
                    return builder;
                }
            }

            if (builder.Exists(typeof(IdAllocator<TId>), includeInterfaceTypes: true, findParentScopes: true))
            {
                // A parent scope has the space; its first value is only readable from the built scope.
                builder.RegisterBuildCallback(scope => IdSpaceRegistration<TId>.InParents(scope)?.Match(first));
                return builder;
            }

            builder.Register(new IdSpaceRegistration<TId>(first));
            return builder;
        }
    }
}
