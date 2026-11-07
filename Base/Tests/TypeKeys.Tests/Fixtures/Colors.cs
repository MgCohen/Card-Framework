using TypeKeys.Domain;
using TypeKeys.Domain.Shared;

namespace TypeKeys.Tests;

public abstract record Color : TypeKey { protected Color(int slot) : base(slot) { } }
public sealed class ColorRegistry : TypeKeyRegistry<Color> { }
public sealed record Red() : Color(1);
public sealed record Green() : Color(2);
public sealed record CustomColor : Color { internal CustomColor(int slot) : base(slot) { } }
