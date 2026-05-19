using System;

namespace Framework.Entities
{
    public readonly struct EntityType : IEquatable<EntityType>
    {
        public int Slot { get; }
        public string Name { get; }

        public EntityType(int slot, string name) { Slot = slot; Name = name; }

        public bool Equals(EntityType other) => Slot == other.Slot;
        public override bool Equals(object obj) => obj is EntityType e && Equals(e);
        public override int GetHashCode() => Slot;
        public override string ToString() => Name;
    }
}
