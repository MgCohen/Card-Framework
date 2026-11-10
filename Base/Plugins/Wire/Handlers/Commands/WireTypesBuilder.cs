using Ecs.Domain.Shared;

namespace Wire.Handlers.Commands;

public sealed class WireTypesBuilder
{
    internal WireTypesBuilder()
    {
    }

    public WireTypesBuilder Component<T>()
        where T : unmanaged, IComponent
    {
        throw new NotImplementedException();
    }

    public WireTypesBuilder Tag<T>()
        where T : struct, ITag
    {
        throw new NotImplementedException();
    }
}
