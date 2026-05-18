#nullable enable
using System;
using NUnit.Framework;

namespace Spike.EntitySetup.Tests
{
    /// <summary>
    /// Construction-flow validation — the three layers (L1 store, L2 From, L3
    /// type-specific Spawn*) compose into the expected tag bag for each kind
    /// of entity the spike supports.
    /// </summary>
    public class SpawnTests
    {
        private static GameEngine NewEngine() => new(new Catalogs());

        private static CardDefinition Fireball()
            => new(101, "Fireball", CardKind.Spell, Cost: 4, Attack: 0, Health: 0,
                   DivineShield: false, Effects: Array.Empty<CardEffect>());

        private static CardDefinition Yeti()
            => new(202, "Chillwind Yeti", CardKind.Minion, Cost: 4, Attack: 4, Health: 5,
                   DivineShield: false, Effects: Array.Empty<CardEffect>());

        private static CardDefinition Squire()
            => new(201, "Argent Squire", CardKind.Minion, Cost: 1, Attack: 1, Health: 1,
                   DivineShield: true, Effects: Array.Empty<CardEffect>());

        // ─── L2: engine.From ──────────────────────────────────────────────

        [Test]
        public void From_StampsIdentityFromDefinition()
        {
            var engine = NewEngine();
            var e = engine.From(Yeti());
            Assert.That(e.Id, Is.GreaterThan(0));
            Assert.That(e.Type, Is.EqualTo(EntityTypes.Card));
            Assert.That(e.DefinitionId, Is.EqualTo(202));
        }

        [Test]
        public void From_DoesNotWriteAnyTags()
        {
            var engine = NewEngine();
            var e = engine.From(Yeti());
            // L2 is plumbing only — tag bag is the L3 helper's job.
            Assert.That(e.Has(Tags.IsCard),  Is.False);
            Assert.That(e.Has(Tags.IsMinion), Is.False);
            Assert.That(e.Has(Tags.Attack),  Is.False);
        }

        // ─── L3: SpawnCard ────────────────────────────────────────────────

        [Test]
        public void SpawnCard_Minion_WritesKindAndStats()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Yeti());

            Assert.That(e.Get(Tags.IsCard),    Is.True);
            Assert.That(e.Get(Tags.IsMinion),  Is.True);
            Assert.That(e.Get(Tags.IsSpell),   Is.False);
            Assert.That(e.Get(Tags.Cost),      Is.EqualTo(4));
            Assert.That(e.Get(Tags.Attack),    Is.EqualTo(4));
            Assert.That(e.Get(Tags.Health),    Is.EqualTo(5));
            Assert.That(e.Get(Tags.MaxHealth), Is.EqualTo(5));
        }

        [Test]
        public void SpawnCard_Spell_OmitsCombatStats()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Fireball());

            Assert.That(e.Get(Tags.IsCard),  Is.True);
            Assert.That(e.Get(Tags.IsSpell), Is.True);
            Assert.That(e.Get(Tags.IsMinion), Is.False);
            Assert.That(e.Get(Tags.Cost),    Is.EqualTo(4));
            // Stats are minion-only — must not be written on a spell.
            Assert.That(e.Has(Tags.Attack),    Is.False);
            Assert.That(e.Has(Tags.Health),    Is.False);
            Assert.That(e.Has(Tags.MaxHealth), Is.False);
        }

        [Test]
        public void SpawnCard_WithoutDivineShield_DoesNotSetFlag()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Yeti());
            Assert.That(e.Has(Tags.DivineShield), Is.False);
        }

        [Test]
        public void SpawnCard_WithDivineShield_SetsFlag()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Squire());
            Assert.That(e.Get(Tags.DivineShield), Is.True);
        }

        [Test]
        public void SpawnCard_WithOwnerAndZone_WritesReferenceIds()
        {
            var engine = NewEngine();
            var owner  = engine.SpawnPlayer();
            var zone   = engine.SpawnHand(owner);
            var card   = engine.SpawnCard(Yeti(), owner: owner, zone: zone);

            Assert.That(card.Get(Tags.Owner), Is.EqualTo(owner.Id));
            Assert.That(card.Get(Tags.Zone),  Is.EqualTo(zone.Id));
        }

        [Test]
        public void SpawnCard_WithoutOwnerOrZone_LeavesRefsUnset()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Yeti());
            Assert.That(e.Has(Tags.Owner), Is.False);
            Assert.That(e.Has(Tags.Zone),  Is.False);
        }

        [Test]
        public void SpawnCard_StampsIdentityFromDef()
        {
            var engine = NewEngine();
            var e = engine.SpawnCard(Squire());
            Assert.That(e.Type, Is.EqualTo(EntityTypes.Card));
            Assert.That(e.DefinitionId, Is.EqualTo(201));
        }

        [Test]
        public void SpawnCard_SameDefTwice_ProducesDistinctEntities()
        {
            var engine = NewEngine();
            var a = engine.SpawnCard(Squire());
            var b = engine.SpawnCard(Squire());
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
            Assert.That(a.DefinitionId, Is.EqualTo(b.DefinitionId));
        }

        // ─── L3: SpawnPlayer ──────────────────────────────────────────────

        [Test]
        public void SpawnPlayer_DefaultsAre30HealthZeroArmor()
        {
            var engine = NewEngine();
            var p = engine.SpawnPlayer();

            Assert.That(p.Type,                Is.EqualTo(EntityTypes.Player));
            Assert.That(p.DefinitionId,        Is.EqualTo(0));
            Assert.That(p.Get(Tags.IsPlayer),  Is.True);
            Assert.That(p.Get(Tags.Health),    Is.EqualTo(30));
            Assert.That(p.Get(Tags.MaxHealth), Is.EqualTo(30));
            Assert.That(p.Get(Tags.Armor),     Is.EqualTo(0));
        }

        [Test]
        public void SpawnPlayer_HonorsHealthAndArmorArgs()
        {
            var engine = NewEngine();
            var p = engine.SpawnPlayer(health: 20, armor: 5);
            Assert.That(p.Get(Tags.Health),    Is.EqualTo(20));
            Assert.That(p.Get(Tags.MaxHealth), Is.EqualTo(20));
            Assert.That(p.Get(Tags.Armor),     Is.EqualTo(5));
        }

        // ─── L3: SpawnHand / Battlefield / Graveyard ──────────────────────

        [Test]
        public void SpawnHand_WritesZoneTypeAndKindMarker()
        {
            var engine = NewEngine();
            var owner  = engine.SpawnPlayer();
            var hand   = engine.SpawnHand(owner);

            Assert.That(hand.Type,                  Is.EqualTo(EntityTypes.Zone));
            Assert.That(hand.Get(Tags.IsZone),      Is.True);
            Assert.That(hand.Get(Tags.IsHand),      Is.True);
            Assert.That(hand.Has(Tags.IsBattlefield), Is.False);
            Assert.That(hand.Has(Tags.IsGraveyard),   Is.False);
            Assert.That(hand.Get(Tags.Owner),       Is.EqualTo(owner.Id));
        }

        [Test]
        public void SpawnBattlefield_WritesKindMarker()
        {
            var engine = NewEngine();
            var owner  = engine.SpawnPlayer();
            var field  = engine.SpawnBattlefield(owner);

            Assert.That(field.Get(Tags.IsZone),         Is.True);
            Assert.That(field.Get(Tags.IsBattlefield),  Is.True);
            Assert.That(field.Has(Tags.IsHand),         Is.False);
            Assert.That(field.Has(Tags.IsGraveyard),    Is.False);
        }

        [Test]
        public void SpawnGraveyard_WritesKindMarker()
        {
            var engine = NewEngine();
            var owner  = engine.SpawnPlayer();
            var grave  = engine.SpawnGraveyard(owner);

            Assert.That(grave.Get(Tags.IsZone),       Is.True);
            Assert.That(grave.Get(Tags.IsGraveyard),  Is.True);
            Assert.That(grave.Has(Tags.IsHand),       Is.False);
            Assert.That(grave.Has(Tags.IsBattlefield), Is.False);
        }

        // ─── End-to-end through the engine + catalog ──────────────────────

        [Test]
        public void Engine_HoldsCatalogsByReference_AcrossUse()
        {
            var catalogs = new Catalogs();
            catalogs.Cards.Add(Squire());
            var engine = new GameEngine(catalogs);

            Assert.That(engine.Catalogs, Is.SameAs(catalogs));
            Assert.That(engine.Catalogs.Cards[201].Name, Is.EqualTo("Argent Squire"));
        }

        [Test]
        public void EveryEntity_LandsInStore()
        {
            var engine = NewEngine();
            var jaina   = engine.SpawnPlayer();
            var jHand   = engine.SpawnHand(jaina);
            var jField  = engine.SpawnBattlefield(jaina);
            var fireball = engine.SpawnCard(Fireball(), jaina, jHand);
            var yeti     = engine.SpawnCard(Yeti(),     jaina, jField);

            var all = engine.EntityStore.All;
            Assert.That(all.Count, Is.EqualTo(5));
            Assert.That(all[jaina.Id],   Is.SameAs(jaina));
            Assert.That(all[jHand.Id],   Is.SameAs(jHand));
            Assert.That(all[jField.Id],  Is.SameAs(jField));
            Assert.That(all[fireball.Id], Is.SameAs(fireball));
            Assert.That(all[yeti.Id],    Is.SameAs(yeti));
        }
    }
}
