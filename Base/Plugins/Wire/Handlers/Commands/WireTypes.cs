namespace Wire.Handlers.Commands;

public sealed class WireTypes
{
    private WireTypes(IReadOnlyList<Type> messages, IReadOnlyList<Type> handlers)
    {
        Messages = messages;
        Handlers = handlers;
    }

    public IReadOnlyList<Type> Messages { get; }

    public IReadOnlyList<Type> Handlers { get; }

    public static WireTypes Of(Action<WireTypesBuilder> build)
    {
        throw new NotImplementedException();
    }
}
