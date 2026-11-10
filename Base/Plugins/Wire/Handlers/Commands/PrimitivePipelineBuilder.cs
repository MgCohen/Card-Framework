using Ecs.Contracts.Commands;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using VSA.Handlers;

namespace Wire.Handlers.Commands;

public sealed class PrimitivePipelineBuilder : IPipelineBuilder<IPrimitive>
{
    public IPipelineBuilder<IPrimitive> Handlers<THandlerBase>()
        where THandlerBase : class
    {
        throw new NotSupportedException();
    }

    public IPipelineBuilder<IPrimitive> HandlersOf(Type handlerBase)
    {
        throw new NotSupportedException();
    }

    public IPipelineBuilder<IPrimitive> Handlers(params Type[] handlers)
    {
        throw new NotSupportedException();
    }

    public IPipelineBuilder<IPrimitive> Runner<TRunner>()
        where TRunner : IRunner
    {
        throw new NotSupportedException();
    }

    public IPipelineBuilder<IPrimitive> BeforeStep<TStep>()
        where TStep : class
    {
        throw new NotSupportedException();
    }

    public IPipelineBuilder<IPrimitive> AfterStep<TStep>()
        where TStep : class
    {
        throw new NotSupportedException();
    }

    public IPipeline<IPrimitive> Build(IServiceProvider services)
    {
        Type[] handlers = WireTypes.BuiltIn.Types
            .Concat(services.GetServices<WireTypes>().SelectMany(t => t.Types))
            .Select(t => t.Handler)
            .Distinct()
            .ToArray();

        return new PipelineBuilder<IPrimitive>()
            .Handlers(handlers)
            .BeforeStep<CreateEntityId>()
            .AfterStep<RecordStep>()
            .Build(services);
    }
}
