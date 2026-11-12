using Ecs.Contracts.Commands;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using VSA.Handlers;

namespace Wire.Handlers.Commands;

public sealed class PrimitivePipelineBuilder : IPipelineBuilder<IPrimitive>
{
    private readonly List<Action<IPipelineBuilder<IPrimitive>>> settings =
        new List<Action<IPipelineBuilder<IPrimitive>>>();

    public IPipelineBuilder<IPrimitive> Handlers<THandlerBase>()
        where THandlerBase : class
    {
        return Add(builder => builder.Handlers<THandlerBase>());
    }

    public IPipelineBuilder<IPrimitive> HandlersOf(Type handlerBase)
    {
        return Add(builder => builder.HandlersOf(handlerBase));
    }

    public IPipelineBuilder<IPrimitive> Handlers(params Type[] handlers)
    {
        return Add(builder => builder.Handlers(handlers));
    }

    public IPipelineBuilder<IPrimitive> Runner<TRunner>()
        where TRunner : IRunner
    {
        return Add(builder => builder.Runner<TRunner>());
    }

    public IPipelineBuilder<IPrimitive> BeforeStep<TStep>()
        where TStep : class
    {
        return Add(builder => builder.BeforeStep<TStep>());
    }

    public IPipelineBuilder<IPrimitive> AfterStep<TStep>()
        where TStep : class
    {
        return Add(builder => builder.AfterStep<TStep>());
    }

    public IPipeline<IPrimitive> Build(IServiceProvider services)
    {
        Type[] handlers = WireTypes.BuiltIn.Types
            .Concat(services.GetServices<WireTypes>().SelectMany(t => t.Types))
            .Select(t => t.Handler)
            .Distinct()
            .ToArray();

        IPipelineBuilder<IPrimitive> builder = new PipelineBuilder<IPrimitive>()
            .Handlers(handlers)
            .BeforeStep<CreateEntityId>();
        foreach (Action<IPipelineBuilder<IPrimitive>> setting in settings)
        {
            setting(builder);
        }

        return builder.AfterStep<RecordStep>().Build(services);
    }

    private PrimitivePipelineBuilder Add(Action<IPipelineBuilder<IPrimitive>> setting)
    {
        settings.Add(setting);
        return this;
    }
}
