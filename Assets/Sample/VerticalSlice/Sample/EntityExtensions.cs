#nullable enable
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Typed accessors for the sample's tags. Hearthstone-style vocabulary:
    /// Health/MaxHealth/Attack/Armor on heroes + minions; abilities as
    /// bool tags.
    ///
    /// Read accessors take <see cref="IEntity"/> (work in any scope, even
    /// effect/hook). Write accessors take <see cref="Entity"/> (require
    /// the mutable view — only available in command scope or setup).
    /// </summary>
    public static class EntityExtensions
    {
        // --- Kind queries (read-only) ----------------------------------------

        public static bool IsCard       (this IEntity e) => e.Has(Tags.IsCard) && e.Get(Tags.IsCard);
        public static bool IsMinion     (this IEntity e) => e.Has(Tags.IsMinion) && e.Get(Tags.IsMinion);
        public static bool IsSpell      (this IEntity e) => e.Has(Tags.IsSpell) && e.Get(Tags.IsSpell);
        public static bool IsZone        (this IEntity e) => e.Has(Tags.IsZone) && e.Get(Tags.IsZone);
        public static bool IsPlayer      (this IEntity e) => e.Has(Tags.IsPlayer) && e.Get(Tags.IsPlayer);
        public static bool IsBattlefield (this IEntity e) => e.Has(Tags.IsBattlefield) && e.Get(Tags.IsBattlefield);
        public static bool IsGraveyard   (this IEntity e) => e.Has(Tags.IsGraveyard) && e.Get(Tags.IsGraveyard);

        // --- Stats (read) ----------------------------------------------------

        public static int    Health        (this IEntity e) => e.Get(Tags.Health);
        public static int    MaxHealth     (this IEntity e) => e.Get(Tags.MaxHealth);
        public static int    Attack        (this IEntity e) => e.Get(Tags.Attack);
        public static int    Armor         (this IEntity e) => e.Get(Tags.Armor);
        public static int    Cost          (this IEntity e) => e.Get(Tags.Cost);
        public static bool   DivineShield  (this IEntity e) => e.Has(Tags.DivineShield) && e.Get(Tags.DivineShield);
        public static bool   Backlash      (this IEntity e) => e.Has(Tags.Backlash) && e.Get(Tags.Backlash);
        // No HasDeathrattle accessor — deathrattle is a direct-trigger
        // attachment, not a tag. Query via CardEffectStore if needed.

        // --- References & names (read) ---------------------------------------

        /// <summary>Card → containing zone's entity id; 0 if not placed.</summary>
        public static int    ZoneId        (this IEntity e) => e.Get(Tags.Zone);

        /// <summary>Card → owning player's entity id; 0 if unowned.</summary>
        public static int    OwnerId       (this IEntity e) => e.Get(Tags.Owner);

        public static string ZoneName      (this IEntity e) => e.Get(Tags.ZoneName) ?? "";
        public static string PlayerName    (this IEntity e) => e.Get(Tags.PlayerName) ?? "";
        public static string CardName      (this IEntity e) => e.Get(Tags.CardName) ?? "";

        // --- Stats (write — Entity required) ---------------------------------

        public static void SetHealth        (this Entity e, int v)    => e.Set(Tags.Health, v);
        public static void SetMaxHealth     (this Entity e, int v)    => e.Set(Tags.MaxHealth, v);
        public static void SetAttack        (this Entity e, int v)    => e.Set(Tags.Attack, v);
        public static void SetArmor         (this Entity e, int v)    => e.Set(Tags.Armor, v);
        public static void SetCost          (this Entity e, int v)    => e.Set(Tags.Cost, v);
        public static void SetDivineShield  (this Entity e, bool v)   => e.Set(Tags.DivineShield, v);
        public static void SetBacklash      (this Entity e, bool v)   => e.Set(Tags.Backlash, v);
        public static void SetCardName      (this Entity e, string v) => e.Set(Tags.CardName, v);

        // --- Kind markers (write — Entity required) --------------------------

        /// <summary>Marks an entity as a card (umbrella). Use
        /// <see cref="MarkAsMinion"/> or <see cref="MarkAsSpell"/> to also
        /// flag the sub-kind.</summary>
        public static void MarkAsCard       (this Entity e) => e.Set(Tags.IsCard, true);

        /// <summary>Card + minion sub-kind. Minions carry battlefield stats.</summary>
        public static void MarkAsMinion     (this Entity e, string name)
        {
            e.Set(Tags.IsCard, true);
            e.Set(Tags.IsMinion, true);
            e.Set(Tags.CardName, name);
        }

        /// <summary>Card + spell sub-kind. Spells are stats-less.</summary>
        public static void MarkAsSpell      (this Entity e, string name)
        {
            e.Set(Tags.IsCard, true);
            e.Set(Tags.IsSpell, true);
            e.Set(Tags.CardName, name);
        }

        public static void MarkAsZone       (this Entity e, string name)
        {
            e.Set(Tags.IsZone, true);
            e.Set(Tags.ZoneName, name);
        }

        public static void MarkAsBattlefield(this Entity e, string name = "Battlefield")
        {
            e.MarkAsZone(name);
            e.Set(Tags.IsBattlefield, true);
        }

        public static void MarkAsGraveyard  (this Entity e, string name = "Graveyard")
        {
            e.MarkAsZone(name);
            e.Set(Tags.IsGraveyard, true);
        }

        /// <summary>Marker-only — the player IS the hero in this slice; use
        /// <see cref="SetHealth"/>/<see cref="SetMaxHealth"/>/<see cref="SetArmor"/>
        /// to give the hero stats.</summary>
        public static void MarkAsPlayer     (this Entity e, string name)
        {
            e.Set(Tags.IsPlayer, true);
            e.Set(Tags.PlayerName, name);
        }

        // --- References (write — Entity required) ----------------------------

        /// <summary>Place a card in a zone. Used by MoveCard.Apply and by setup.</summary>
        public static void SetZone          (this Entity e, IEntity zone) => e.Set(Tags.Zone, zone.Id);

        /// <summary>Assign a card or zone to a player.</summary>
        public static void SetOwner         (this Entity e, IEntity player) => e.Set(Tags.Owner, player.Id);
    }
}
