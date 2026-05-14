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
    /// Deathrattle = MoveCard's After+Reaction trigger. Fires only when the
    /// moved card has the HasDeathrattle tag AND the destination zone is
    /// tagged IsGraveyard. Body deals 2 damage to all enemy cards (cards
    /// owned by a different player than the source).
    /// </summary>
    public class DeathrattleTests
    {
        // --- Helper: minimal multi-player setup -------------------------------

        private (GameEngine eng, Entity p1, Entity p2,
                 Entity battlefield, Entity graveyard,
                 Entity casterP1, Entity allyP1,
                 Entity targetP2, Entity allyP2) BuildBoard()
        {
            var eng = new GameEngine();
            DeathrattleRule.Register(eng.Commands);

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
        public void Deathrattle_MoveToGraveyard_DamagesAllEnemies_NotAllies()
        {
            var b = BuildBoard();
            b.casterP1.SetHasDeathrattle(true);

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            // P2's cards take 2 damage each
            Assert.That(b.targetP2.Hp(), Is.EqualTo(28));
            Assert.That(b.allyP2.Hp(), Is.EqualTo(8));
            // P1's cards untouched
            Assert.That(b.allyP1.Hp(), Is.EqualTo(10));
            // Source's HP untouched (we don't damage the dying card itself)
            Assert.That(b.casterP1.Hp(), Is.EqualTo(30));
        }

        [Test]
        public void Deathrattle_OpensNestedBlock_NamedDeathrattle_WithFanOut()
        {
            var b = BuildBoard();
            b.casterP1.SetHasDeathrattle(true);

            int checkpoint = b.eng.World.Log.Count;
            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            var blockNames = b.eng.World.Log.Events.Skip(checkpoint)
                              .OfType<BlockStartEvent>()
                              .Select(bs => bs.Name)
                              .ToList();

            // Expected nesting:
            //   MoveCard → Deathrattle → DealDamage (enemy 1) → DealDamage (enemy 2)
            Assert.That(blockNames, Is.EqualTo(new[]
            {
                "MoveCard", "Deathrattle", "DealDamage", "DealDamage",
            }));
        }

        [Test]
        public void Deathrattle_DoesNotFire_WhenCardLacksTag()
        {
            var b = BuildBoard();
            // No SetHasDeathrattle on caster.

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.allyP2.Hp(), Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_DoesNotFire_OnNonGraveyardMove()
        {
            // Card has Deathrattle but moves to a non-graveyard zone.
            var b = BuildBoard();
            b.casterP1.SetHasDeathrattle(true);
            var hand = b.eng.World.Create(); hand.MarkAsZone("Hand");

            b.eng.Commands.Execute(new MoveCard(b.casterP1, hand));

            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.allyP2.Hp(), Is.EqualTo(10));
        }

        [Test]
        public void Deathrattle_RoutesThroughDealDamage_TriggersDivineShield()
        {
            // Even though the damage source is a deathrattle, it goes through
            // DealDamage like any other damage. Hooks attached to DealDamage
            // (Shield, Armor) fire normally.
            var b = BuildBoard();
            DivineShieldRule.Register(b.eng.Commands);
            b.casterP1.SetHasDeathrattle(true);
            b.targetP2.SetDivineShield(true);

            b.eng.Commands.Execute(new MoveCard(b.casterP1, b.graveyard));

            // Target's shield absorbs the deathrattle hit; ally still takes it.
            Assert.That(b.targetP2.Hp(), Is.EqualTo(30));
            Assert.That(b.targetP2.DivineShield(), Is.False);
            Assert.That(b.allyP2.Hp(), Is.EqualTo(8));
        }

        [Test]
        public void Deathrattle_OnlyDamagesEnemies_RegardlessOfOwnership()
        {
            // P2 dies — only P1's cards should be hit (the "enemy" perspective
            // flips). Caster's deathrattle hits P2; target's deathrattle hits P1.
            var b = BuildBoard();
            b.targetP2.SetHasDeathrattle(true);

            b.eng.Commands.Execute(new MoveCard(b.targetP2, b.graveyard));

            Assert.That(b.casterP1.Hp(), Is.EqualTo(28));
            Assert.That(b.allyP1.Hp(), Is.EqualTo(8));
            Assert.That(b.allyP2.Hp(), Is.EqualTo(10));
        }
    }
}
