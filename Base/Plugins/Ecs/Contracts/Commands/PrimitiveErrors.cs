using VSA.Contracts;

namespace Ecs.Contracts.Commands;

public static class PrimitiveErrors
{
    public static readonly Error Unchanged = new Error("Primitive.Unchanged", "The World already has this state.");

    public static readonly Error EntityMissing = new Error("Primitive.EntityMissing", "No live entity has this id.");

    public static readonly Error EntityExists = new Error("Primitive.EntityExists", "A live entity has this id.");
}
