using Framework.Core;

namespace Framework.Entities
{
    public abstract record EntityType(int Slot) : TypeKey(Slot);

    public sealed record Unknown()  : EntityType(0);
    public sealed record Player()   : EntityType(2);
    public sealed record Zone()     : EntityType(3);
    public sealed record Card()     : EntityType(4);
    public sealed record Modifier() : EntityType(5);
}
