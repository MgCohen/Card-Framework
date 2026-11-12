using System.Reflection;
using System.Runtime.ExceptionServices;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
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

    private static MethodInfo OfDefinition { get; } =
        new Func<WireType>(Of<CreateEntity, EntityId, CreateEntityHandler>).Method.GetGenericMethodDefinition();

    public Type Message { get; }

    public Type Handler { get; }

    public static WireType Of<TPrimitive, TResult, THandler>()
        where TPrimitive : class, IPrimitive<TResult>
    {
        LoadFormatter<TPrimitive>();
        return new WireType(typeof(TPrimitive), typeof(THandler), SendAsync<TPrimitive, TResult>);
    }

    public static WireType OfHandler(Type handler)
    {
        Type primitiveBase = PrimitiveBaseOf(handler);
        Type[] arguments = primitiveBase.GetGenericArguments();
        MethodInfo of = OfDefinition.MakeGenericMethod(arguments[0], arguments[1], handler);
        try
        {
            return (WireType)of.Invoke(null, null)!;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    public Task<Error?> SendAsync(IPrimitive primitive, ICommandSender bus, CancellationToken ct)
    {
        return send(primitive, bus, ct);
    }

    private static Type PrimitiveBaseOf(Type handler)
    {
        for (Type? type = handler.BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PrimitiveHandler<,>))
            {
                return type;
            }
        }

        throw new InvalidOperationException($"{handler.FullName} does not derive from PrimitiveHandler<,>.");
    }

    private static void LoadFormatter<T>()
        where T : class
    {
        MemoryPackSerializer.Serialize<T>(null);
    }

    private static async Task<Error?> SendAsync<TPrimitive, TResult>(
        IPrimitive primitive,
        ICommandSender bus,
        CancellationToken ct)
        where TPrimitive : class, IPrimitive<TResult>
    {
        Result<TResult> result = await bus.SendAsync((TPrimitive)primitive, ct);
        return result.Error;
    }
}
