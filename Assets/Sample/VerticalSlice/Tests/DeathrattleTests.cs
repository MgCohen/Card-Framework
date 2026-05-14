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
            var p2 = eng.World.Create(); p2.MarkAsPlayer("P2");

            var bf = eng.World.Create(); bf.MarkAsZone("Battlefield");
            var grave = eng.World.Create(); grave.MarkAsGraveyard();

            var caster = eng.World.Create();
            caster.MarkAsCard(); caster.SetCardName("CasterP1");
            caster.SetHp(30); caster.SetMaxHp(30); caster.SetOwner(p1); caster.SetZone(bf);

            var allyP1 = eng.World.Create();
            allyP1.MarkAsCard(); allyP1.SetCardName("AllyP1");
            allyP1.SetHp(10); allyP1.SetMaxHp(10); allyP1.SetOwner(p1); allyP1.SetZone(bf);

            var target = eng.World.Create();
            target.MarkAsCard(); target.SetCardName("TargetP2");
            target.SetHp(30); target.SetMaxHp(30); target.SetOwner(p2); target.SetZone(bf);

            var allyP2 = eng.World.Create();
            allyP2.MarkAsCard(); allyP2.SetCardName("AllyP2");
            allyP2.SetHp(10); allyP2.SetMaxHp(10); allyP2.SetOwner(p2); allyP2.SetZone(bf);

            return (eng, p1, p2, bf, grave, caster, allyP1, target, allyP2);
        }

        [Test]
        public void Deathrattle_DamageAllEnemies_FiresOnGraveyardMove()
        {
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            // P2's cards took 2 damage each:
            Assert.That(b.targetP2.Hp(), Is.EqualTo(28));
            Assert.That(b.allyP2.Hp(),   Is.EqualTo(8));
            // P1's other card untouched:
            Assert.That(b.allyP1.Hp(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_HealAllAllies_FiresOnSameTrigger_DifferentEffect()
        {
            // Same bridge / same trigger / different card → different effect.
            // This is the "per-card variation" the whole CardEffectStore exists
            // for — attaching a totally different effect to a different card
            // requires no new C# subclass, no new bridge, no rule changes.
            var b = BuildBoard();
            b.allyP1.SetHp(7);   // hurt so the heal is observable
            b.eng.CardEffects.Set<Deathrattle>(b.allyP1,
                t => new HealAllAllies(t));

            b.eng.Commands.Execute(new MoveCard(b.allyP1, b.graveyard));

            // P1's other card (caster) gets +3 hp (capped at MaxHp):
            Assert.That(b.casterP1.Hp(), Is.EqualTo(30));   // already at max
            // The dying card is excluded from the heal (no point):
            Assert.That(b.allyP1.Hp(),   Is.EqualTo(7));
            // P2's cards untouched:
            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.allyP2.Hp(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_HealAllAllies_HealsBelowMax()
        {
            var b = BuildBoard();
            b.casterP1.SetHp(20);   // hurt
            b.eng.CardEffects.Set<Deathrattle>(b.allyP1,
                t => new HealAllAllies(t));

            b.eng.Commands.Execute(new MoveCard(b.allyP1, b.graveyard));

            Assert.That(b.casterP1.Hp(), Is.EqualTo(23));   // 20 + 3
        }

        [Test]
        public void Deathrattle_DoesNotFire_WhenCardHasNoEffectAttached()
        {
            // Target has no deathrattle in the store → bridge is a no-op
            // for this card. No retaliation, no heal, no effect.
            var b = BuildBoard();

            b.eng.Commands.Execute(new MoveCard(b.targetP2, b.graveyard));

            Assert.That(b.casterP1.Hp(), Is.EqualTo(30));
            Assert.That(b.allyP1.Hp(),   Is.EqualTo(10));
            Assert.That(b.allyP2.Hp(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_DoesNotFire_OnNonGraveyardMove()
        {
            var b = BuildBoard();
            b.eng.CardEffects.Set<Deathrattle>(b.casterP1,
                t => new DamageAllEnemies(t));
            var hand = b.eng.World.Create(); hand.MarkAsZone("Hand");

            b.eng.Commands.Execute(new MoveCard(b.casterP1, hand));

            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.allyP2.Hp(),   Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_NestedBlock_AppearsInLog()
        {
            // Wire shape on a damage-all-enemies deathrattle:
            //   Command(MoveCard) → Effect(DamageAllEnemies)
            //     → Command(DealDamage)... × N
            // No "DeathrattleBridge" wire block — the bridge is a plain bus
            // subscription, not an Effect. Per the design we chose to keep
            // the wire clean of plumbing.
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
                "MoveCard", "DamageAllEnemies", "DealDamage", "DealDamage",
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

            // Target absorbed; ally took it.
            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.targetP2.DivineShield(), Is.False);
            Assert.That(b.allyP2.Hp(), Is.EqualTo(8));
        }
    }
}
