using System;
using Framework.RecordIds;
using Framework.Routing;
using VContainer;

namespace Framework.Pipeline
{
    public static class PipelineInstall
    {
        public static IContainerBuilder AddPipeline<TPipeline, TMessage, TScope>(this IContainerBuilder builder,
            Action<PipelineOptions> configure = null)
            where TPipeline : Pipeline<TMessage, TScope>
            where TMessage : Message
        {
            var options = new PipelineOptions();
            configure?.Invoke(options);

            // A client has no MessageId space, so a client pipeline could not give ids.
            if (options.Role != Role.Server)
                throw new InvalidOperationException(
                    $"{typeof(TPipeline).Name} runs only on the server. The client has no MessageId space.");

            // Two pipelines can share a router; a second scan would register each member twice.
            AddRouterOnce<Handler<TMessage, TScope>>(builder, options.IncludeTests);
            AddRouterOnce<Validator<TMessage>>(builder, options.IncludeTests);
            builder.AddIdSpace<MessageId>(options.Role, options.FirstMessageId);
            if (!builder.Exists(typeof(TPipeline), includeInterfaceTypes: true, findParentScopes: true))
                builder.Register<TPipeline>(Lifetime.Singleton);
            return builder;
        }

        static void AddRouterOnce<T>(IContainerBuilder builder, bool includeTests) where T : class, IRouted
        {
            if (!builder.Exists(typeof(TypeRouter<T>), includeInterfaceTypes: true, findParentScopes: true))
                builder.RegisterTypeRouter<T>(RouteMode.Single, includeTests: includeTests);
        }
    }
}
