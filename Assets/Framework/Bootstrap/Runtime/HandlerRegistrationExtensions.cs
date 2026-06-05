using Framework.Commands;
using Framework.Tags;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    public static class HandlerRegistrationExtensions
    {
        public static RegistrationBuilder RegisterHandler<T>(this IContainerBuilder builder)
            where T : IWireEventHandler
            => builder.Register<T>(Lifetime.Singleton).AsImplementedInterfaces();

        public static RegistrationBuilder RegisterCommandHandler<T>(this IContainerBuilder builder)
            where T : ICommandHandler
            => builder.Register<T>(Lifetime.Singleton).AsImplementedInterfaces();

        public static RegistrationBuilder RegisterCommandValidator<T>(this IContainerBuilder builder)
            where T : ICommandValidator
            => builder.Register<T>(Lifetime.Singleton).AsImplementedInterfaces();

        public static IContainerBuilder RegisterTagValueType<T>(this IContainerBuilder builder)
            where T : unmanaged
        {
            builder.RegisterHandler<TagSetHandler<T>>();
            builder.RegisterHandler<TagChangeHandler<T>>();
            return builder;
        }
    }
}
