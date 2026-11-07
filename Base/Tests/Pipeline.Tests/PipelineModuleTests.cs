using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pipeline.Slices.Run;
using VSA.Composition;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;
using VSA.Module;

namespace Pipeline.Tests;

public sealed class PipelineModuleTests
{
    [Test]
    public void Composer_ReplacesTheCommandSender()
    {
        using ServiceProvider provider = Compose(new ServiceCollection());
        using IServiceScope scope = provider.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<ICommandSender>(), Is.InstanceOf<PipelineCommandSender>());
        Assert.That(scope.ServiceProvider.GetRequiredService<PipelineServices>(), Is.Not.Null);
        Assert.That(provider.GetRequiredService<PipelineValidators>(), Is.Not.Null);
    }

    [Test]
    public async Task Composer_ALayerOfAModule_RunsACommand()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(new Log());
        services.AddSingleton(new RecordingWriter());
        services.AddSingleton<PipelineLayer>(Layers.Test());
        services.AddScoped<CommandHandler<Ping, string>, PingHandler>();
        using ServiceProvider provider = Compose(services);
        using IServiceScope scope = provider.CreateScope();

        string result = await scope.ServiceProvider.GetRequiredService<ICommandSender>().SendAsync(new Ping(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("pong"));
        Assert.That(provider.GetRequiredService<Log>().Steps, Is.EqualTo(new[] { "scope", "before", "handle", "after" }));
    }

    private static ServiceProvider Compose(ServiceCollection services)
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        VsaComposer composer = new VsaComposer(new MicrosoftServices(services), configuration, new PipelineModule());
        composer.Install();
        return services.BuildServiceProvider();
    }
}
