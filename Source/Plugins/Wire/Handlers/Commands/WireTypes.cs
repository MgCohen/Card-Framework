using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using VSA.Contracts;

namespace Wire.Handlers.Commands;

public sealed class WireTypes
{
    internal static readonly WireTypes BuiltIn = new WireTypes(new[]
    {
        WireType.Of<CreateEntity, EntityId, CreateEntityHandler>(),
        WireType.Of<DeleteEntity, Unit, DeleteEntityHandler>(),
    });

    private WireTypes(IReadOnlyList<WireType> types)
        : this(types, Array.Empty<WireMessage>())
    {
    }

    private WireTypes(IReadOnlyList<WireType> types, IReadOnlyList<WireMessage> messages)
    {
        Types = types;
        Messages = messages;
    }

    internal IReadOnlyList<WireType> Types { get; }

    internal IReadOnlyList<WireMessage> Messages { get; }

    public static WireTypes OfHandlers(IEnumerable<Type> handlers)
    {
        return new WireTypes(handlers.Select(WireType.OfHandler).ToArray());
    }

    // The client message types of the closed command handlers. A handler of another message gives nothing.
    public static WireTypes OfCommandHandlers(IEnumerable<Type> handlers)
    {
        WireMessage[] messages = handlers
            .Select(WireMessage.OfHandler)
            .Where(m => m is not null)
            .Select(m => m!)
            .ToArray();
        return new WireTypes(Array.Empty<WireType>(), messages);
    }

    public static WireTypes Of(Action<WireTypesBuilder> build)
    {
        WireTypesBuilder builder = new WireTypesBuilder();
        build(builder);
        return new WireTypes(builder.Types.ToArray(), builder.Messages.ToArray());
    }
}
