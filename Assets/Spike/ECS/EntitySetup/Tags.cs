#nullable enable

namespace Spike.EntitySetup
{
    /// <summary>
    /// Tag inventory for the setup spike. No <c>CardName</c> /
    /// <c>ZoneName</c> / <c>PlayerName</c> — content names live on defs
    /// and the UI reads them via <c>catalog.Cards[entity.DefinitionId]</c>;
    /// zone "name" comes from the kind marker (IsHand / IsBattlefield /
    /// IsGraveyard) as a presentation constant.
    ///
    /// Slot ranges by convention:
    ///   1-19   kind markers (booleans identifying the category)
    ///   20-39  stats (numeric values that gameplay reads/modifies)
    ///   40-59  ability flags
    ///   80-99  reference tags (pointing at another entity's id)
    /// </summary>
    public static class Tags
    {
        // --- Kind markers (1-19) ----------------------------------------
        public static readonly TagKey<bool> IsCard        = new( 1, "isCard");
        public static readonly TagKey<bool> IsMinion      = new( 2, "isMinion");
        public static readonly TagKey<bool> IsSpell       = new( 3, "isSpell");
        public static readonly TagKey<bool> IsZone        = new( 4, "isZone");
        public static readonly TagKey<bool> IsHand        = new( 5, "isHand");
        public static readonly TagKey<bool> IsBattlefield = new( 6, "isBattlefield");
        public static readonly TagKey<bool> IsGraveyard   = new( 7, "isGraveyard");
        public static readonly TagKey<bool> IsPlayer      = new( 8, "isPlayer");

        // --- Stats (20-39) ----------------------------------------------
        public static readonly TagKey<int>  Cost          = new(20, "cost");
        public static readonly TagKey<int>  Attack        = new(21, "attack");
        public static readonly TagKey<int>  Health        = new(22, "health");
        public static readonly TagKey<int>  MaxHealth     = new(23, "maxHealth");
        public static readonly TagKey<int>  Armor         = new(24, "armor");

        // --- Ability flags (40-59) --------------------------------------
        public static readonly TagKey<bool> DivineShield  = new(40, "divineShield");

        // --- References (80-99) -----------------------------------------
        public static readonly TagKey<int>  Owner         = new(80, "owner");
        public static readonly TagKey<int>  Zone          = new(81, "zone");
    }
}
