using Microsoft.Extensions.DependencyInjection;
using Pipeline.Handlers.Commands;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class Harness : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;

    public Harness(params PipelineLayer[] layers)
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(Log);
        services.AddSingleton(Writer);
        services.AddSingleton(Mana);
        AddHandlers(services);
        services.AddSingleton<Validator>(r => new PingValidator(Log));
        services.AddSingleton<Validator>(r => new DeniedValidator(Log, Mana));
        services.AddSingleton<Validator>(r => new CrashValidator(Log));
        services.AddSingleton<PipelineValidators>();
        services.AddSingleton<PipelineRuns>();
        services.AddScoped(r => new PipelineServices(r.GetService));
        foreach (PipelineLayer layer in layers)
        {
            services.AddSingleton<PipelineLayer>(layer);
        }

        services.AddScoped<ICommandSender, PipelineCommandSender>();
        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
    }

    public Log Log { get; } = new Log();

    public RecordingWriter Writer { get; } = new RecordingWriter();

    public Mana Mana { get; } = new Mana();

    public HoldHandler Hold { get; } = new HoldHandler();

    public KeepHandler Keep { get; } = new KeepHandler();

    public PipelineServices Services => scope.ServiceProvider.GetRequiredService<PipelineServices>();

    public ICommandSender Sender => scope.ServiceProvider.GetRequiredService<ICommandSender>();

    public static async Task<Exception?> Catch(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
    }

    private void AddHandlers(ServiceCollection services)
    {
        services.AddScoped<CommandHandler<Ping, string>, PingHandler>();
        services.AddScoped<CommandHandler<Echo, int>, EchoHandler>();
        services.AddScoped<CommandHandler<Boom, int>, BoomHandler>();
        services.AddScoped<CommandHandler<Denied, int>, DeniedHandler>();
        services.AddSingleton<CommandHandler<Hold, int>>(Hold);
        services.AddScoped<CommandHandler<Write, bool>, WriteHandler>();
        services.AddSingleton<CommandHandler<Keep, int>>(Keep);
        services.AddScoped<CommandHandler<Nest, string>, NestHandler>();
        services.AddScoped<CommandHandler<NestTwice, string>, NestTwiceHandler>();
        services.AddScoped<CommandHandler<Cross, int>, CrossHandler>();
        services.AddScoped<CommandHandler<Count, int>, CountHandler>();
        services.AddScoped<CommandHandler<Both, int>, BothHandler>();
        services.AddScoped<CommandHandler<Misfit, int>, MisfitHandler>();
        services.AddScoped<CommandHandler<Loose, int>, LooseHandler>();
        services.AddScoped<CommandHandler<Plain, int>, PlainHandler>();
    }
}
