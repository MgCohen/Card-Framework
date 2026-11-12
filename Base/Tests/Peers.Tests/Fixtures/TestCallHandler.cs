using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers;
using VSA.Handlers.Commands;
using Wire.Handlers.Commands;

namespace Peers.Tests.Fixtures;

public sealed class TestCallHandler : CommandHandler<TestCall, int>
{
    public static readonly Error Refused = new Error("Tests.Refused", "The test handler refused the call.");

    private readonly ICommandSender commands;

    public TestCallHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    // The host does not scan test assemblies, so each peer adds the message to its catalog and its pipeline here.
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton(WireTypes.Of(t => t.ClientMessage<TestCall, int>()));
        services.AddPipeline(new PipelineBuilder<TestCall>().Handlers(typeof(TestCallHandler)));
    }

    public override async Task<Result<int>> HandleAsync(Envelope<TestCall> envelope)
    {
        TestCall call = envelope.Message;
        switch (call.Mode)
        {
            case CallMode.Echo:
                return call.Value;
            case CallMode.Refuse:
                return Refused;
            case CallMode.Throw:
                throw new InvalidOperationException("The test handler throws.");
            default:
                Result<EntityId> created = await commands.SendAsync(new CreateEntity(default), envelope.Cancellation);
                return created.IsSuccess ? created.Value.Value : created.Error!;
        }
    }
}
