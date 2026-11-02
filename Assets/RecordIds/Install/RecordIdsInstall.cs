using VContainer;

namespace Framework.RecordIds
{
    public static class RecordIdsInstall
    {
        // Only the server gives ids; the client reads them from the wire.
        public static IContainerBuilder AddIdSpace<TId>(this IContainerBuilder builder, Role role, int first = 1)
            where TId : Id, new()
        {
            if (role != Role.Server) return builder;

            // Each system declares the id spaces it uses, so two systems can declare the same one.
            if (builder.Exists(typeof(IdAllocator<TId>))) return builder;

            builder.RegisterInstance(new IdAllocator<TId>(first));
            return builder;
        }
    }
}
