#nullable enable
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Typed accessors for the sample's tags. Replaces the per-subclass
    /// typed properties (<c>card.Hp</c>) we had before with extension
    /// methods on <see cref="IEntity"/> / <see cref="Entity"/>.
    ///
    /// Read accessors take <see cref="IEntity"/> (work in any scope, even
    /// effect/hook). Write accessors take <see cref="Entity"/> (require
    /// the mutable view — only available in command scope or setup).
    /// </summary>
    public static class EntityExtensions
    {
        // --- Kind queries (read-only) ----------------------------------------

        public static bool IsCard(this IEntity e) => e.Has(Tags.IsCard) && e.Get(Tags.IsCard);
        public static bool IsZone(this IEntity e) => e.Has(Tags.IsZone) && e.Get(Tags.IsZone);

        // --- Stats (read) ----------------------------------------------------

        public static int    Hp          (this IEntity e) => e.Get(Tags.Hp);
        public static int    MaxHp       (this IEntity e) => e.Get(Tags.MaxHp);
        public static int    Attack      (this IEntity e) => e.Get(Tags.Attack);
        public static int    Armor       (this IEntity e) => e.Get(Tags.Armor);
        public static bool   DivineShield(this IEntity e) => e.Has(Tags.DivineShield) && e.Get(Tags.DivineShield);
        public static bool   Backlash    (this IEntity e) => e.Has(Tags.Backlash) && e.Get(Tags.Backlash);

        // --- Zone (read) -----------------------------------------------------

        /// <summary>Card → containing zone's entity id; 0 if not placed.</summary>
        public static int    ZoneId      (this IEntity e) => e.Get(Tags.Zone);

        /// <summary>Zone → display label; empty string if not set.</summary>
        public static string ZoneName    (this IEntity e) => e.Get(Tags.ZoneName) ?? "";

        // --- Stats (write — Entity required) ---------------------------------

        public static void SetHp          (this Entity e, int v)    => e.Set(Tags.Hp, v);
        public static void SetMaxHp       (this Entity e, int v)    => e.Set(Tags.MaxHp, v);
        public static void SetAttack      (this Entity e, int v)    => e.Set(Tags.Attack, v);
        public static void SetArmor       (this Entity e, int v)    => e.Set(Tags.Armor, v);
        public static void SetDivineShield(this Entity e, bool v)   => e.Set(Tags.DivineShield, v);
        public static void SetBacklash    (this Entity e, bool v)   => e.Set(Tags.Backlash, v);

        public static void MarkAsCard     (this Entity e)           => e.Set(Tags.IsCard, true);
        public static void MarkAsZone     (this Entity e, string name)
        {
            e.Set(Tags.IsZone, true);
            e.Set(Tags.ZoneName, name);
        }

        /// <summary>Place a card in a zone. Used by MoveCard.Apply and by setup.</summary>
        public static void SetZone        (this Entity e, IEntity zone) => e.Set(Tags.Zone, zone.Id);
    }
}
