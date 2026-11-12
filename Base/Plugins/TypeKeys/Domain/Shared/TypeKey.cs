namespace TypeKeys.Domain.Shared;

public abstract record TypeKey(int Slot) : ITypeKey
{
    internal int Slot { get; init; } = Slot;
}
