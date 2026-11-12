using TypeKeys.Domain;
using TypeKeys.Domain.Shared;

namespace TypeKeys.Fixtures;

public abstract record Suit : TypeKey { protected Suit(int slot) : base(slot) { } }
public sealed class SuitRegistry : TypeKeyRegistry<Suit> { }
public sealed record Hearts() : Suit(1);
public sealed record Spades() : Suit(2);
