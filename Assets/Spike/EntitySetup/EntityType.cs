#nullable enable
using System;

namespace Spike.EntitySetup
{
    /// <summary>
    /// Catalog partition + first-class entity kind. Struct-wrapped int with
    /// a debug-only name. TagKey-style open registry — games extend by
    /// declaring more <see cref="EntityType"/> instances as
    /// <c>static readonly</c> fields, no enum required.
    /// </summary>
    public readonly struct EntityType : IEquatable<EntityType>
    {
        public int    Value { get; }
        public string Name  { get; }

        public EntityType(int value, string name) { Value = value; Name = name; }

        public bool Equals(EntityType other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EntityType e && Equals(e);
        public override int GetHashCode() => Value;
        public override string ToString() => Name;
    }

    /// <summary>
    /// The framework's bundled types. Games extend with their own static
    /// readonly fields (Token, Aura, etc.). Modifier is declared here but
    /// the modifier shape itself is parked — see the modifier sketch.
    /// </summary>
    public static class EntityTypes
    {
        public static readonly EntityType Card     = new(1, "Card");
        public static readonly EntityType Zone     = new(2, "Zone");
        public static readonly EntityType Player   = new(3, "Player");
        public static readonly EntityType Modifier = new(4, "Modifier");   // parked
    }
}
