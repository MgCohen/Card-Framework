using Ecs.Contracts.Commands;
using MemoryPack;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Wire.Handlers.Commands;

internal sealed class WireType
{
    private readonly Func<IPrimitive, ICommandSender, CancellationToken, Task<Error?>> send;

    private WireType(Type message, Type handler, Func<IPrimitive, ICommandSender, CancellationToken, Task<Error?>> send)
    {
        Message = message;
        Handler = handler;
        this.send = send;
    }

    public Type Message { get; }

    public Type Handler { get; }

    public static WireType Of<TPrimitive, TResult, THandler>()
        where TPrimitive : class, IPrimitive<TResult>
    {
        LoadFormatter<TPrimitive>();
        return new WireType(typeof(TPrimitive), typeof(THandler), SendAsync<TPrimitive, TResult>);
    }

    public Task<Error?> SendAsync(IPrimitive primitive, ICommandSender bus, CancellationToken ct)
    {
        return send(primitive, bus, ct);
    }

    private static void LoadFormatter<T>()
        where T : class
    {
        MemoryPackSerializer.Serialize<T>(null);
    }

    private static async Task<Error?> SendAsync<TPrimitive, TResult>(IPrimitive primitive, ICommandSender bus, CancellationToken ct)
        where TPrimitive : class, IPrimitive<TResult>
    {
        Result<TResult> result = await bus.SendAsync((TPrimitive)primitive, ct);
        return result.Error;
    }
}
