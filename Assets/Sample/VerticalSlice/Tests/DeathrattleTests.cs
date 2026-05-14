#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    /// <summary>
    /// Deathrattle as a direct trigger: per-card attached effect, looked
    /// up via CardEffectStore when the engine bridge detects a card
    /// moving into a graveyard zone. Different cards can have totally
    /// different deathrattle effects — proven here by attaching
    /// <see cref="DamageAllEnemies"/> to one card and
    /// <see cref="HealAllAllies"/> to another.
    ///
    /// AOE filter: DamageAllEnemies targets enemy hero + enemy minions on
    /// a battlefield zone. HealAllAllies targets friendly minions on a
    /// battlefield zone (skip dying self). The board fixture uses
    /// MarkAsMinion + MarkAsBattlefield so the in-play filter fires.
    /// </summary>
    public class DeathrattleTests
    {
        private (GameEngine eng, Entity p1, Entity p2,
                 Entity battlefield, Entity graveyard,
                 Entity casterP1, Entity allyP1,
                 Entity targetP2, Entity allyP2) BuildBoard()
        {
            var eng = new GameEngine();
            DeathrattleBridge.Register(eng.Commands);

            var p1 = eng.World.Create(); p1.MarkAsPlayer("P1");
            p1.SetHealth(30); p1.SetMaxHealth(30);

            var p2 = eng.World.Create(); p2.MarkAsPlayer("P2");
            p2.SetHealth(30); p2.SetMaxHealth(30);

            var bf = eng.World.Create(); bf.MarkAsBattlefield("Battlefield");
            var grave = eng.World.Create(); grave.MarkAsGraveyard();

            var caster = eng.World.Create();
            caster.MarkAsMinion("CasterP1");
            caster.SetHealth(30); caster.SetMaxHealth(30); caster.SetOwner(p1); caster.SetZone(bf);

            var allyP1 = eng.World.Create();
            allyP1.MarkAsMinion("AllyP1");
            allyP1.SetHealth(10); allyP1.SetMaxHealth(10); allyP1.SetOwner(p1); allyP1.SetZone(bf);

            var target = eng.World.Create();
            target.MarkAsMinion("TargetP2");
            target.SetHealth(30); target.SetMaxHealth(30); target.SetOwner(p2); target.SetZone(bf);

            var allyP2 = eng.World.Create();
            allyP2.MarkAsMinion("AllyP2");
            allyP2.SetHealth(10); allyP2.SetMaxHealth(10); allyP2.SetOwner(p2); allyP2.SetZone(bf);

            return (eng, p1, p2, bf, grave, caster, allyP1, target, allyP2);
        }

        [Test]
        public void Deathrattle_DamageAllEnemies_FiresOnGraveyardMove()
        {
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            // Enemy hero + every enemy minion on the battlefield take 2:
            Assert.That(b.p2.Health(),       Is.EqualTo(28));
            Assert.That(b.targetP2.Health(), Is.EqualTo(28));
            Assert.That(b.allyP2.Health(),   Is.EqualTo(8));
            // Friendly side untouched:
            Assert.That(b.p1.Health(),       Is.EqualTo(30));
            Assert.That(b.allyP1.Health(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_HealAllAllies_FiresOnSameTrigger_DifferentEffect()
        {
            // Same bridge / same trigger / different card → different effect.
            // This is the "per-card variation" the whole CardEffectStore exists
            // for — attaching a totally different effect to a different card
            // requires no new C# subclass, no new bridge, no rule changes.
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.allyP1,
                t => new HealAllAllies(t));

            b.eng.Commands.Execute(new MoveCard(b.allyP1, b.graveyard));

            // CasterP1 (friendly minion on battlefield) heals 3 — already at max.
            Assert.That(b.casterP1.Health(), Is.EqualTo(30));
            // Dying minion excluded from the heal:
            Assert.That(b.allyP1.Health(),   Is.EqualTo(10));
            // Friendly hero is NOT a minion — HealAllAllies skips it.
            Assert.That(b.p1.Health(),       Is.EqualTo(30));
            // Enemy minions untouched:
            Assert.That(b.targetP2.Health(), Is.EqualTo(30));
            Assert.That(b.allyP2.Health(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_HealAllAllies_HealsBelowMax()
        {
            var b = BuildBoard();
            b.casterP1.SetHealth(20);   // hurt
            b.eng.CardEffects.Set<Deathrattle>(b.allyP1,
                t => new HealAllAllies(t));

            b.eng.Commands.Execute(new MoveCard(b.allyP1, b.graveyard));

            Assert.That(b.casterP1.Health(), Is.EqualTo(23));   // 20 + 3
        }

        [Test]
        public void Deathrattle_DoesNotFire_WhenCardHasNoEffectAttached()
        {
            // Target has no deathrattle in the store → bridge is a no-op
            // for this card. No retaliation, no heal, no effect.
            var b = BuildBoard();

            b.eng.Commands.Execute(new MoveCard(b.targetP2, b.graveyard));

            Assert.That(b.casterP1.Health(), Is.EqualTo(30));
            Assert.That(b.allyP1.Health(),   Is.EqualTo(10));
            Assert.That(b.allyP2.Health(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_DoesNotFire_OnNonGraveyardMove()
        {
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));
            var hand = b.eng.World.Create(); hand.MarkAsZone("Hand");

            b.eng.Commands.Execute(new MoveCard(b.casterP1, hand));

            Assert.That(b.p2.Health(),       Is.EqualTo(30));
            Assert.That(b.targetP2.Health(), Is.EqualTo(30));
            Assert.That(b.allyP2.Health(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_NestedBlock_AppearsInLog()
        {
            // Wire shape on a damage-all-enemies deathrattle:
            //   Command(MoveCard) → Effect(DamageAllEnemies)
            //     → Command(DealDamage)... × N
            // No "DeathrattleBridge" wire block — the bridge is a plain bus
            // subscription, not an Effect. Per the design we chose to keep
            // the wire clean of plumbing. Three DealDamage children: enemy
            // hero + two enemy minions.
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));

            int checkpoint = b.eng.World.Log.Count;
            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            var blockNames = b.eng.World.Log.Events.Skip(checkpoint)
                              .OfType<BlockStartEvent>()
                              .Select(bs => bs.Name)
                              .ToList();
            Assert.That(blockNames, Is.EqualTo(new[]
            {
                "MoveCard", "DamageAllEnemies", "DealDamage", "DealDamage", "DealDamage",
            }));
        }

        [Test]
        public void Deathrattle_RoutesThroughDealDamage_TriggersDivineShield()
        {
            // Deathrattle's damage goes through DealDamage like any other
            // damage. Hooks attached to DealDamage (Shield) fire normally.
            var b = BuildBoard();
            DivineShieldRule.Register(b.eng.Effects);
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));
            b.targetP2.SetDivineShield(true);

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            // Target absorbed; hero + other minion still took 2.
            Assert.That(b.targetP2.Health(),       Is.EqualTo(30));
            Assert.That(b.targetP2.DivineShield(), Is.False);
            Assert.That(b.allyP2.Health(),         Is.EqualTo(8));
            Assert.That(b.p2.Health(),             Is.EqualTo(28));
        }
    }
}
