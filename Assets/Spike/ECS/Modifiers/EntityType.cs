#nullable enable
using System;

namespace Spike.Modifiers
{
    public readonly struct EntityType : IEquatable<EntityType>
    {
        public int    Slot { get; }
        public string Name { get; }

        public EntityType(int slot, string name) { Slot = slot; Name = name; }

        public bool Equals(EntityType other) => Slot == other.Slot;
        public override bool Equals(object? obj) => obj is EntityType e && Equals(e);
        public override int GetHashCode() => Slot;
        public override string ToString() => Name;
    }

    // Engine-reserved slots follow the 2026-05-18 registry closure
    // (definition-entity-setup.md): 2 Player, 3 Zone, 4 Card, 5 Modifier.
    public static class EntityTypes
    {
        public static readonly EntityType Player   = new(2, "Player");
        public static readonly EntityType Zone     = new(3, "Zone");
        public static readonly EntityType Card     = new(4, "Card");
        public static readonly EntityType Modifier = new(5, "Modifier");
    }
}
