#nullable enable

namespace Spike.EntitySetup
{
    // ─── Layer 3 — type-specific spawn helpers ─────────────────────────
    //
    // Extension methods on GameEngine. The framework ships a few; games
    // add their own following the same pattern, no inheritance, no
    // registration, no plumbing. Each helper does three things in order:
    //
    //   1. Create the entity (via L1 or L2 depending on whether there's
    //      a def to read).
    //   2. Write the base value tags + placement tags.
    //   3. Wire non-tag concerns (effects, subscriptions) into other
    //      stores keyed by the entity's id.
    //
    // Effects (step 3) are stubbed for the spike — registration
    // mechanics parked, see the spike doc.

    public static class CardSpawn
    {
        public static Entity SpawnCard(this GameEngine engine, CardDefinition def,
                                       Entity? owner = null, Entity? zone = null)
        {
            var e = engine.From(def);                           // L2: id + type + defId

            // Base values from the def → tags
            e.Set(Tags.IsCard, true);
            switch (def.Kind)
            {
                case CardKind.Minion: e.Set(Tags.IsMinion, true); break;
                case CardKind.Spell:  e.Set(Tags.IsSpell,  true); break;
            }
            e.Set(Tags.Cost, def.Cost);
            if (def.Kind == CardKind.Minion)
            {
                e.Set(Tags.Attack,    def.Attack);
                e.Set(Tags.Health,    def.Health);
                e.Set(Tags.MaxHealth, def.Health);
            }
            if (def.DivineShield) e.Set(Tags.DivineShield, true);

            // Placement from spawn args
            if (owner != null) e.Set(Tags.Owner, owner.Id);
            if (zone  != null) e.Set(Tags.Zone,  zone.Id);

            // Non-tag concerns — effects would register into an EffectStore
            // here. Stubbed; see spike doc / modifier sketch.
            // foreach (var fx in def.Effects) engine.EffectStore.Register(e, fx);

            return e;
        }
    }

    public static class PlayerSpawn
    {
        // Defless — players don't have defs yet (Q4 / "Player needs a def"
        // follow-up flagged in the spike doc). Conceptually complete shape
        // still earns its helper.
        public static Entity SpawnPlayer(this GameEngine engine,
                                         int health = 30, int armor = 0)
        {
            var e = engine.EntityStore.Create(EntityTypes.Player);
            e.Set(Tags.IsPlayer,  true);
            e.Set(Tags.Health,    health);
            e.Set(Tags.MaxHealth, health);
            e.Set(Tags.Armor,     armor);
            return e;
        }
    }

    public static class ZoneSpawn
    {
        // Defless — three "shape" helpers, one per zone kind. Names come
        // from the kind marker, not a string tag.
        public static Entity SpawnHand       (this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsHand);
        public static Entity SpawnBattlefield(this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsBattlefield);
        public static Entity SpawnGraveyard  (this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsGraveyard);

        private static Entity MakeZone(GameEngine engine, Entity owner, TagKey<bool> kindMarker)
        {
            var e = engine.EntityStore.Create(EntityTypes.Zone);
            e.Set(Tags.IsZone, true);
            e.Set(kindMarker,  true);
            e.Set(Tags.Owner,  owner.Id);
            return e;
        }
    }
}
