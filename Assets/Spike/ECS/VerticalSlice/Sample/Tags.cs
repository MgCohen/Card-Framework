#nullable enable
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// All sample tags. Slots are author-assigned at declaration —
    /// analyzer-checked unique. Slot ranges are an organizational
    /// convention:
    /// <list type="bullet">
    /// <item>1-19 — kind markers</item>
    /// <item>20-39 — stats</item>
    /// <item>40-59 — plain ability flags</item>
    /// <item>60-79 — names</item>
    /// <item>80-99 — entity-id refs</item>
    /// <item>100+ — Pattern B keywords (declared via <c>[TagSlot]</c> on
    /// the keyword class itself, not here)</item>
    /// </list>
    ///
    /// <para>Wire keys remain explicit strings — renaming the C# field
    /// doesn't break the wire format. The Name→Slot wire migration is a
    /// deferred follow-up spike.</para>
    /// </summary>
    public static class Tags
    {
        // --- Kind markers (1-19) -----------------------------------------
        public static readonly TagKey<bool>   IsCard         = new( 1, "isCard");
        public static readonly TagKey<bool>   IsMinion       = new( 2, "isMinion");
        public static readonly TagKey<bool>   IsSpell        = new( 3, "isSpell");
        public static readonly TagKey<bool>   IsZone         = new( 4, "isZone");
        public static readonly TagKey<bool>   IsPlayer       = new( 5, "isPlayer");
        public static readonly TagKey<bool>   IsBattlefield  = new( 6, "isBattlefield");
        public static readonly TagKey<bool>   IsGraveyard    = new( 7, "isGraveyard");

        // --- Stats (20-39) -----------------------------------------------
        public static readonly TagKey<int>    Health         = new(20, "health");
        public static readonly TagKey<int>    MaxHealth      = new(21, "maxHealth");
        public static readonly TagKey<int>    Attack         = new(22, "attack");
        public static readonly TagKey<int>    Armor          = new(23, "armor");
        public static readonly TagKey<int>    Cost           = new(24, "cost");

        // --- Plain ability flags (40-59) ---------------------------------
        // Pattern A bool tags that don't double as event payload classes.
        // For "keyword + payload" abilities (Deathrattle, future Poison),
        // see Pattern B — the keyword class itself carries [TagSlot].
        public static readonly TagKey<bool>   DivineShield   = new(40, "divineShield");
        public static readonly TagKey<bool>   Backlash       = new(41, "backlash");

        // --- Names (60-79) -----------------------------------------------
        public static readonly TagKey<string> ZoneName       = new(60, "zoneName");
        public static readonly TagKey<string> PlayerName     = new(61, "playerName");
        public static readonly TagKey<string> CardName       = new(62, "cardName");

        // --- References / entity-id pointers (80-99) ---------------------
        public static readonly TagKey<int>    Zone           = new(80, "zone");      // card → containing zone
        public static readonly TagKey<int>    Owner          = new(81, "owner");     // card → owning player

        /// <summary>
        /// Register the slice's tag indices on an <see cref="EntityStore"/>,
        /// plus the Pattern B keyword types this slice uses. Call once at
        /// engine setup, before any entities are created.
        ///
        /// <para>Marker indices: kind tags + zone-kind tags. Anything we
        /// routinely ask "show me all X" about. Plain ability flags
        /// (DivineShield, Backlash) aren't indexed because we don't query
        /// them collectively — they're per-entity gates inside hooks.</para>
        ///
        /// <para>Value indices: owner + zone — the two reference-tag
        /// pivots ("entities owned by P1", "entities in zone Z") that
        /// queries compound against most often.</para>
        ///
        /// <para>Pattern B: registers Deathrattle's synthesized
        /// <c>TagKey&lt;bool&gt;</c> + indexes it so
        /// <c>store.With(Tag&lt;Deathrattle&gt;.Key)</c> works.</para>
        /// </summary>
        public static void RegisterIndices(EntityStore store)
        {
            // Pattern A markers
            store.IndexMarker(IsCard);
            store.IndexMarker(IsMinion);
            store.IndexMarker(IsSpell);
            store.IndexMarker(IsZone);
            store.IndexMarker(IsPlayer);
            store.IndexMarker(IsBattlefield);
            store.IndexMarker(IsGraveyard);

            // Pattern A value indices
            store.IndexBy(Owner);
            store.IndexBy(Zone);

            // Pattern B keywords — register the type (synthesizes the
            // TagKey<bool> at the declared slot), then index it for
            // world.With queries.
            store.IndexMarker((TagKey<bool>)Tag<Deathrattle>.Key);
        }
    }
}
