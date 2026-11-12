using TypeKeys.Domain.Shared;

namespace TypeKeys.Tests;

public abstract record Fruit : TypeKey { protected Fruit(int slot) : base(slot) { } }
public sealed record Apple() : Fruit(1);
