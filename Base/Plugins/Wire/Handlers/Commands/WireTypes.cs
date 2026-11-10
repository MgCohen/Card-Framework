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
    {
        Types = types;
    }

    internal IReadOnlyList<WireType> Types { get; }

    public static WireTypes Of(Action<WireTypesBuilder> build)
    {
        WireTypesBuilder builder = new WireTypesBuilder();
        build(builder);
        return new WireTypes(builder.Types.ToArray());
    }
}
