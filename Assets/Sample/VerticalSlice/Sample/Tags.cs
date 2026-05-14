#nullable enable
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// All sample tags. Wire keys are explicit strings (no implicit
    /// derivation from C# names) so renaming the C# field doesn't break
    /// the wire format.
    ///
    /// "Kind markers" (<see cref="IsCard"/>) are bool tags — entity-as-card
    /// is just a marker tag, not a C# subclass. Entities can carry
    /// multiple markers (e.g. a card AND a token).
    /// </summary>
    public static class Tags
    {
        // Kind markers — what is this entity?
        public static readonly TagKey<bool>   IsCard         = new("isCard");
        public static readonly TagKey<bool>   IsZone         = new("isZone");
        public static readonly TagKey<bool>   IsPlayer       = new("isPlayer");
        public static readonly TagKey<bool>   IsGraveyard    = new("isGraveyard");  // semantic zone-kind marker

        // Card stats
        public static readonly TagKey<int>    Hp             = new("hp");
        public static readonly TagKey<int>    MaxHp          = new("maxHp");
        public static readonly TagKey<int>    Attack         = new("attack");
        public static readonly TagKey<int>    Armor          = new("armor");

        // Card flags
        public static readonly TagKey<bool>   DivineShield   = new("divineShield");
        public static readonly TagKey<bool>   Backlash       = new("backlash");
        // Note: there is no "hasDeathrattle" tag. Deathrattle is a direct
        // trigger attached via CardEffectStore — the presence of an
        // attached effect IS the source of truth. Avoids the previous
        // tag-vs-handler sync bug.

        // Names
        public static readonly TagKey<string> ZoneName       = new("zoneName");
        public static readonly TagKey<string> PlayerName     = new("playerName");
        public static readonly TagKey<string> CardName       = new("cardName");

        // References (entity id pointers — flat ECS)
        public static readonly TagKey<int>    Zone           = new("zone");      // card → containing zone
        public static readonly TagKey<int>    Owner          = new("owner");     // card → owning player
    }
}
