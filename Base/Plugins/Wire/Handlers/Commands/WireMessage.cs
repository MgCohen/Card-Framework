using System.Reflection;
using System.Runtime.ExceptionServices;
using MemoryPack;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;
using Wire.Contracts.Commands;

namespace Wire.Handlers.Commands;

// One client message type in the catalog: it runs the message on a bus and packs the result into a WireReply.
internal sealed class WireMessage
{
    private readonly Func<IMessage, ICommandSender, CancellationToken, Task<WireReply>> run;

    private WireMessage(Type message, Func<IMessage, ICommandSender, CancellationToken, Task<WireReply>> run)
    {
        Message = message;
        this.run = run;
    }

    private static MethodInfo OfDefinition { get; } =
        new Func<WireMessage>(Of<NoMessage, int>).Method.GetGenericMethodDefinition();

    public Type Message { get; }

    public static WireMessage Of<TMessage, TReply>()
        where TMessage : class, IClientMessage<TReply>
    {
        MemoryPackSerializer.Serialize<TMessage>(null);
        return new WireMessage(typeof(TMessage), RunAsync<TMessage, TReply>);
    }

    // A closed command handler gives a client message type if its message implements IClientMessage of its result.
    public static WireMessage? OfHandler(Type handler)
    {
        for (Type? type = handler.BaseType; type is not null; type = type.BaseType)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(CommandHandler<,>))
            {
                continue;
            }

            Type[] arguments = type.GetGenericArguments();
            if (!typeof(IClientMessage<>).MakeGenericType(arguments[1]).IsAssignableFrom(arguments[0]))
            {
                return null;
            }

            try
            {
                return (WireMessage)OfDefinition.MakeGenericMethod(arguments).Invoke(null, null)!;
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        return null;
    }

    public Task<WireReply> RunAsync(IMessage message, ICommandSender bus, CancellationToken ct)
    {
        return run(message, bus, ct);
    }

    private static async Task<WireReply> RunAsync<TMessage, TReply>(
        IMessage message,
        ICommandSender bus,
        CancellationToken ct)
        where TMessage : class, IClientMessage<TReply>
    {
        Result<TReply> result = await bus.SendAsync((TMessage)message, ct);
        return result.IsSuccess
            ? new WireReply(null, null, MemoryPackSerializer.Serialize(result.Value))
            : new WireReply(result.Error!.Code, result.Error.Message, Array.Empty<byte>());
    }

    // Only gives the generic method definition. It is never sent.
    private sealed class NoMessage : IClientMessage<int>
    {
    }
}
