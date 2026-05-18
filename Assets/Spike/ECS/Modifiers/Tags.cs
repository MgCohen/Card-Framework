#nullable enable

namespace Spike.Modifiers
{
    // Slot ranges by convention:
    //   1-19   kind markers (sub-kind within an EntityType)
    //   20-39  stats — both rebaseable (Attack/MaxHealth/Cost) and transient (Health/Shields)
    //   40-59  ability flags (transient by default)
    //   80-99  references (entity ids)
    //   100+   modifier-specific (ModTarget, ModSource)
    public static class Tags
    {
        // Sub-kinds
        public static readonly TagKey<bool> IsMinion      = new( 1, "isMinion");
        public static readonly TagKey<bool> IsSpell       = new( 2, "isSpell");
        public static readonly TagKey<bool> IsHand        = new( 3, "isHand");
        public static readonly TagKey<bool> IsBattlefield = new( 4, "isBattlefield");
        public static readonly TagKey<bool> IsGraveyard   = new( 5, "isGraveyard");

        // Stats — rebaseable (Attack, MaxHealth, Cost, Defense)
        public static readonly TagKey<int>  Cost          = new(20, "cost");
        public static readonly TagKey<int>  Attack        = new(21, "attack");
        public static readonly TagKey<int>  MaxHealth     = new(22, "maxHealth");
        public static readonly TagKey<int>  Defense       = new(23, "defense");

        // Stats — transient (Health, Armor)
        public static readonly TagKey<int>  Health        = new(30, "health");
        public static readonly TagKey<int>  Armor         = new(31, "armor");

        // Ability flags — transient
        public static readonly TagKey<bool> DivineShield  = new(40, "divineShield");

        // References
        public static readonly TagKey<int>  Owner         = new(80, "owner");
        public static readonly TagKey<int>  Zone          = new(81, "zone");

        // Modifier wiring
        public static readonly TagKey<int>  ModTarget     = new(100, "modTarget");
        public static readonly TagKey<int>  ModSource     = new(101, "modSource");
    }
}
