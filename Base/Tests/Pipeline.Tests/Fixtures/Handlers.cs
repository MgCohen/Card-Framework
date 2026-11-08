using Pipeline.Handlers.Commands;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class PingHandler : Handler<Ping, string, TestScope>
{
    protected override Task<string> HandleAsync(Ping command, TestScope scope, CancellationToken ct)
    {
        scope.Log.Steps.Add("handle");
        return Task.FromResult("pong");
    }
}

public sealed class EchoHandler : Handler<Echo, int, TestScope>
{
    protected override Task<int> HandleAsync(Echo command, TestScope scope, CancellationToken ct)
    {
        return Task.FromResult(command.Value);
    }
}

public sealed class BoomHandler : Handler<Boom, int, TestScope>
{
    protected override Task<int> HandleAsync(Boom command, TestScope scope, CancellationToken ct)
    {
        scope.Log.Steps.Add("handle");
        throw new InvalidOperationException("boom");
    }
}

public sealed class DeniedHandler : Handler<Denied, int, TestScope>
{
    protected override Task<int> HandleAsync(Denied command, TestScope scope, CancellationToken ct)
    {
        scope.Log.Steps.Add("handle");
        return Task.FromResult(1);
    }
}

public sealed class HoldHandler : Handler<Hold, int, TestScope>
{
    public TaskCompletionSource<int> Release { get; } = new TaskCompletionSource<int>();

    protected override Task<int> HandleAsync(Hold command, TestScope scope, CancellationToken ct)
    {
        return Release.Task;
    }
}

public sealed class WriteHandler : Handler<Write, bool, TestScope>
{
    protected override Task<bool> HandleAsync(Write command, TestScope scope, CancellationToken ct)
    {
        scope.Writer.Set(command.Value);
        return Task.FromResult(true);
    }
}

public sealed class KeepHandler : Handler<Keep, int, TestScope>
{
    public TestScope? Kept { get; private set; }

    protected override Task<int> HandleAsync(Keep command, TestScope scope, CancellationToken ct)
    {
        Kept = scope;
        return Task.FromResult(0);
    }
}

public sealed class NestHandler : Handler<Nest, string, TestScope>
{
    private readonly ICommandSender commands;

    public NestHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    protected override Task<string> HandleAsync(Nest command, TestScope scope, CancellationToken ct)
    {
        return commands.SendAsync(new Ping(), ct);
    }
}

public sealed class NestTwiceHandler : Handler<NestTwice, string, TestScope>
{
    private readonly ICommandSender commands;

    public NestTwiceHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    protected override async Task<string> HandleAsync(NestTwice command, TestScope scope, CancellationToken ct)
    {
        string first = await commands.SendAsync(new Ping(), ct);
        string second = await commands.SendAsync(new Ping(), ct);
        return first + second;
    }
}

public sealed class CrossHandler : Handler<Cross, int, TestScope>
{
    private readonly ICommandSender commands;

    public CrossHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    protected override Task<int> HandleAsync(Cross command, TestScope scope, CancellationToken ct)
    {
        return commands.SendAsync(new Count(command.Value), ct);
    }
}

public sealed class CountHandler : Handler<Count, int, OtherScope>
{
    protected override Task<int> HandleAsync(Count command, OtherScope scope, CancellationToken ct)
    {
        return Task.FromResult(command.Value * 2);
    }
}

public sealed class BothHandler : Handler<Both, int, TestScope>
{
    protected override Task<int> HandleAsync(Both command, TestScope scope, CancellationToken ct)
    {
        return Task.FromResult(0);
    }
}

public sealed class MisfitHandler : Handler<Misfit, int, OtherScope>
{
    protected override Task<int> HandleAsync(Misfit command, OtherScope scope, CancellationToken ct)
    {
        return Task.FromResult(0);
    }
}

public sealed class LooseHandler : CommandHandler<Loose, int>
{
    public override Task<int> HandleAsync(Loose command, CancellationToken ct)
    {
        return Task.FromResult(0);
    }
}

public sealed class PlainHandler : CommandHandler<Plain, int>
{
    public override Task<int> HandleAsync(Plain command, CancellationToken ct)
    {
        return Task.FromResult(command.Value + 1);
    }
}
