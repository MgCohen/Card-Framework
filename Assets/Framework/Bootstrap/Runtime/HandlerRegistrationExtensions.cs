using Framework.States;
using Framework.Tags;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    // DI sugar for registering wire handlers. RegisterHandler<T> ensures the handler is
    // exposed against the IWireEventHandler marker so HandlerRegistry's IReadOnlyList ctor
    // injection picks it up. RegisterTagValueType<T> is per-tag-value-type — one call per
    // T (int, bool, ...) wires both the Set and Change handlers for that T. RegisterStateType<T>
    // is the state sibling — one call per GameState wires its StatePushHandler.
    public static class HandlerRegistrationExtensions
    {
        public static RegistrationBuilder RegisterHandler<T>(this IContainerBuilder builder)
            where T : IWireEventHandler
            => builder.Register<T>(Lifetime.Singleton).AsImplementedInterfaces();

        public static IContainerBuilder RegisterTagValueType<T>(this IContainerBuilder builder)
            where T : unmanaged
        {
            builder.RegisterHandler<TagSetHandler<T>>();
            builder.RegisterHandler<TagChangeHandler<T>>();
            return builder;
        }

        public static IContainerBuilder RegisterStateType<TState>(this IContainerBuilder builder)
            where TState : GameState, new()
        {
            builder.RegisterHandler<StatePushHandler<TState>>();
            return builder;
        }
    }
}
