#nullable enable
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// All sample tags. Wire keys are explicit strings (no implicit
    /// derivation from C# names) so renaming the C# field doesn't break
    /// the wire format.
    ///
    /// Hearthstone vocabulary: Health/MaxHealth/Attack/Armor on heroes
    /// and minions; abilities (DivineShield/Backlash) as bool flags;
    /// Deathrattle is NOT a tag — it's a CardEffectStore entry.
    ///
    /// "Kind markers" (<see cref="IsCard"/>, <see cref="IsMinion"/>, etc.)
    /// are bool tags — entity-as-card is just a marker tag, not a C#
    /// subclass. Entities can carry multiple markers (e.g. a card AND
    /// a minion, since IsCard is the umbrella for any card-class entity).
    /// </summary>
    public static class Tags
    {
        // Kind markers — what is this entity?
        // Card umbrella + sub-kinds (minion / spell).
        public static readonly TagKey<bool>   IsCard         = new("isCard");
        public static readonly TagKey<bool>   IsMinion       = new("isMinion");
        public static readonly TagKey<bool>   IsSpell        = new("isSpell");
        public static readonly TagKey<bool>   IsZone         = new("isZone");
        public static readonly TagKey<bool>   IsPlayer       = new("isPlayer");   // player IS the hero in this slice
        public static readonly TagKey<bool>   IsBattlefield  = new("isBattlefield");
        public static readonly TagKey<bool>   IsGraveyard    = new("isGraveyard");

        // Character stats (heroes + minions both carry these)
        public static readonly TagKey<int>    Health         = new("health");
        public static readonly TagKey<int>    MaxHealth      = new("maxHealth");
        public static readonly TagKey<int>    Attack         = new("attack");
        public static readonly TagKey<int>    Armor          = new("armor");

        // Card stats
        public static readonly TagKey<int>    Cost           = new("cost");        // mana cost (cosmetic — no economy yet)

        // Ability flags
        public static readonly TagKey<bool>   DivineShield   = new("divineShield");
        public static readonly TagKey<bool>   Backlash       = new("backlash");
        // Note: there is no "hasDeathrattle" tag. Deathrattle is a direct
        // trigger attached via CardEffectStore — the presence of an
        // attached effect IS the source of truth.

        // Names
        public static readonly TagKey<string> ZoneName       = new("zoneName");
        public static readonly TagKey<string> PlayerName     = new("playerName");
        public static readonly TagKey<string> CardName       = new("cardName");

        // References (entity id pointers — flat ECS)
        public static readonly TagKey<int>    Zone           = new("zone");      // card → containing zone
        public static readonly TagKey<int>    Owner          = new("owner");     // card → owning player
    }
}
