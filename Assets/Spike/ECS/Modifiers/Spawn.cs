#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Spike.Modifiers
{
    // Layer 3 — type-specific spawn helpers. Cards mirror rebaseable stats
    // through SetBase so refresh has somewhere to rewind to; transient
    // stats use Set.
    public static class CardSpawn
    {
        public static Entity SpawnCard(this GameEngine engine, CardDefinition def,
                                       Entity? owner = null, Entity? zone = null)
        {
            var e = engine.From(def);

            switch (def.Kind)
            {
                case CardKind.Minion: e.Set(Tags.IsMinion, true); break;
                case CardKind.Spell:  e.Set(Tags.IsSpell,  true); break;
            }

            // Rebaseable stats — SetBase so refresh can reset to these.
            e.SetBase(Tags.Cost, def.Cost);
            if (def.Kind == CardKind.Minion)
            {
                e.SetBase(Tags.Attack,    def.Attack);
                e.SetBase(Tags.MaxHealth, def.Health);
                if (def.Defense > 0) e.SetBase(Tags.Defense, def.Defense);

                // Transient — current health starts equal to max but is not
                // touched by refresh. Gameplay damage subtracts here.
                e.Set(Tags.Health, def.Health);
            }
            if (def.DivineShield) e.Set(Tags.DivineShield, true);

            if (owner != null) e.Set(Tags.Owner, owner.Id);
            if (zone  != null) e.Set(Tags.Zone,  zone.Id);
            return e;
        }
    }

    public static class PlayerSpawn
    {
        public static Entity SpawnPlayer(this GameEngine engine,
                                         int health = 30, int armor = 0)
        {
            var e = engine.EntityStore.Create(EntityTypes.Player);
            e.SetBase(Tags.MaxHealth, health);
            e.Set    (Tags.Health,    health);
            e.Set    (Tags.Armor,     armor);
            return e;
        }
    }

    public static class ZoneSpawn
    {
        public static Entity SpawnHand       (this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsHand);
        public static Entity SpawnBattlefield(this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsBattlefield);
        public static Entity SpawnGraveyard  (this GameEngine engine, Entity owner) => MakeZone(engine, owner, Tags.IsGraveyard);

        private static Entity MakeZone(GameEngine engine, Entity owner, TagKey<bool> kindMarker)
        {
            var e = engine.EntityStore.Create(EntityTypes.Zone);
            e.Set(kindMarker, true);
            e.Set(Tags.Owner, owner.Id);
            return e;
        }
    }

    // ─── Modifier engine ─────────────────────────────────────────────────

    public static class ModifierEngine
    {
        public static Entity SpawnModifier(this GameEngine engine,
                                           ModifierDefinition def,
                                           Entity target,
                                           Entity? source = null)
        {
            // L2 stamps id + Type=Modifier + DefinitionId.
            var m = engine.From(def);
            m.Set(Tags.ModTarget, target.Id);
            if (source != null) m.Set(Tags.ModSource, source.Id);

            // One-shot transient writes (DS grant, current-health bump).
            def.OnAttach?.Apply(target, engine.Effects.NewScope());

            // Replay every rebaseable contribution from base.
            engine.RefreshModifiers(target);
            return m;
        }

        public static void DestroyModifier(this GameEngine engine, Entity modifier)
        {
            int targetId = modifier.Get(Tags.ModTarget);
            engine.EntityStore.Destroy(modifier.Id);

            var target = engine.EntityStore.Get(targetId);
            if (target != null) engine.RefreshModifiers(target);
        }

        // Refresh: reset rebaseable slots to Base, then replay every active
        // modifier's Refresh effect in Order. Transient slots untouched.
        public static void RefreshModifiers(this GameEngine engine, Entity target)
        {
            // Reset pass — only rebaseable slots. Slots without a base entry
            // can't be reset; conventionally they are transient anyway.
            foreach (var (slot, baseValue) in target.Base)
            {
                if (TagPolicies.RebaseOf(slot) == RebasePolicy.Rebaseable)
                    target.SetSlotRaw(slot, baseValue);
            }

            // Replay pass — every modifier targeting this entity, in Order.
            var scope = engine.Effects.NewScope();
            foreach (var m in engine.OrderedModifiersFor(target))
            {
                if (engine.Catalogs.Modifiers.TryGet(m.DefinitionId, out var def))
                    def.Refresh?.Apply(target, scope);
            }
        }

        public static IEnumerable<Entity> OrderedModifiersFor(this GameEngine engine,
                                                              Entity target)
        {
            return engine.EntityStore
                .ModifiersTargeting(target.Id)
                .Select(m => (modifier: m,
                              order: engine.Catalogs.Modifiers.TryGet(m.DefinitionId, out var def)
                                     ? def.Order : 0,
                              tiebreak: m.Id))
                .OrderBy(t => t.order).ThenBy(t => t.tiebreak)
                .Select(t => t.modifier);
        }

        public static void RemoveModifiersFromSource(this GameEngine engine, Entity source)
        {
            foreach (var m in engine.EntityStore.ModifiersFromSource(source.Id).ToList())
                engine.DestroyModifier(m);
        }

        public static void RemoveModifiersTargeting(this GameEngine engine, Entity target)
        {
            foreach (var m in engine.EntityStore.ModifiersTargeting(target.Id).ToList())
                engine.DestroyModifier(m);
        }
    }
}
