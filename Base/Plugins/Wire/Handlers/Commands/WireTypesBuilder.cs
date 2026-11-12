using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using VSA.Contracts;
using Wire.Contracts.Commands;

namespace Wire.Handlers.Commands;

public sealed class WireTypesBuilder
{
    internal WireTypesBuilder()
    {
    }

    internal List<WireType> Types { get; } = new List<WireType>();

    public WireTypesBuilder Component<T>()
        where T : struct, IComponent
    {
        Types.Add(WireType.Of<ChangeComponent<T>, T, ChangeComponentHandler<T>>());
        Types.Add(WireType.Of<RemoveComponent<T>, Unit, RemoveComponentHandler<T>>());
        return this;
    }

    public WireTypesBuilder ClientMessage<TMessage, TReply>()
        where TMessage : class, IClientMessage<TReply>
    {
        return this;
    }

    public WireTypesBuilder Tag<T>()
        where T : struct, ITag
    {
        Types.Add(WireType.Of<AddTag<T>, Unit, AddTagHandler<T>>());
        Types.Add(WireType.Of<RemoveTag<T>, Unit, RemoveTagHandler<T>>());
        return this;
    }
}
