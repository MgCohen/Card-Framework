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
    /// DeathSBA — the state-based action that moves any minion at Health ≤ 0
    /// from a battlefield zone to its owner's graveyard. Runs in the
    /// engine's drain loop after every top-level dispatch.
    /// </summary>
    public class DeathSBATests
    {
        private (GameEngine eng, Entity p1, Entity p2,
                 Entity p1Bf, Entity p1Grave,
                 Entity p2Bf, Entity p2Grave) BuildBoard(bool withDeathrattles = false)
        {
            var eng = new GameEngine();
            DeathrattleBridge.Register(eng.EffectStore);
            DeathSBA.Register(eng);

            var p1 = eng.World.Create(); p1.MarkAsPlayer("P1"); p1.SetHealth(30); p1.SetMaxHealth(30);
            var p2 = eng.World.Create(); p2.MarkAsPlayer("P2"); p2.SetHealth(30); p2.SetMaxHealth(30);

            var p1Bf = eng.World.Create(); p1Bf.MarkAsBattlefield("P1 Battlefield"); p1Bf.SetOwner(p1);
            var p1Grave = eng.World.Create(); p1Grave.MarkAsGraveyard("P1 Graveyard"); p1Grave.SetOwner(p1);
            var p2Bf = eng.World.Create(); p2Bf.MarkAsBattlefield("P2 Battlefield"); p2Bf.SetOwner(p2);
            var p2Grave = eng.World.Create(); p2Grave.MarkAsGraveyard("P2 Graveyard"); p2Grave.SetOwner(p2);

            return (eng, p1, p2, p1Bf, p1Grave, p2Bf, p2Grave);
        }

        [Test]
        public void Minion_AtZeroHealth_MovesToOwnerGraveyard_AfterDispatch()
        {
            var b = BuildBoard();
            var minion = b.eng.World.Create();
            minion.MarkAsMinion("Doomed"); minion.SetOwner(b.p1); minion.SetZone(b.p1Bf);
            minion.SetHealth(2); minion.SetMaxHealth(2);

            // Cast Fireball — 6 dmg, no armor, no shield, no backlash. Minion
            // hits -4 HP. After the cast's inline subtree completes the drain
            // pass picks up the dying minion and moves it.
            b.eng.Stack.Push(new StackFrame(new Fireball(b.p2, minion)));

            Assert.That(minion.Health(), Is.EqualTo(-4));
            Assert.That(minion.ZoneId(), Is.EqualTo(b.p1Grave.Id),
                "DeathSBA must move 0-HP minion to its owner's graveyard.");
        }

        [Test]
        public void SBA_PicksOwnerSpecificGraveyard()
        {
            // P1's minion goes to P1's graveyard, not P2's. The lookup is
            // by owner-id match, not by "first graveyard".
            var b = BuildBoard();

            var p1Minion = b.eng.World.Create();
            p1Minion.MarkAsMinion("P1 Doomed"); p1Minion.SetOwner(b.p1); p1Minion.SetZone(b.p1Bf);
            p1Minion.SetHealth(0); p1Minion.SetMaxHealth(5);

            var p2Minion = b.eng.World.Create();
            p2Minion.MarkAsMinion("P2 Doomed"); p2Minion.SetOwner(b.p2); p2Minion.SetZone(b.p2Bf);
            p2Minion.SetHealth(0); p2Minion.SetMaxHealth(5);

            // Issue any command — the drain pass should sweep both dying minions.
            // Use a no-op move on a non-existent target… actually we need any
            // top-level dispatch. Use Fireball with Amount-zero edge: skip,
            // and just dispatch a trivial command instead.
            var bystander = b.eng.World.Create(); bystander.MarkAsCard();
            b.eng.Enqueue(new MoveCard(bystander, b.p1Bf));

            Assert.That(p1Minion.ZoneId(), Is.EqualTo(b.p1Grave.Id));
            Assert.That(p2Minion.ZoneId(), Is.EqualTo(b.p2Grave.Id));
        }

        [Test]
        public void SBA_DoesNotMove_LivingMinions()
        {
            var b = BuildBoard();
            var alive = b.eng.World.Create();
            alive.MarkAsMinion("Alive"); alive.SetOwner(b.p1); alive.SetZone(b.p1Bf);
            alive.SetHealth(3); alive.SetMaxHealth(3);

            // Trigger a drain pass via any top-level dispatch.
            var bystander = b.eng.World.Create(); bystander.MarkAsCard();
            b.eng.Enqueue(new MoveCard(bystander, b.p1Bf));

            Assert.That(alive.ZoneId(), Is.EqualTo(b.p1Bf.Id),
                "Living minions stay on the battlefield.");
        }

        [Test]
        public void SBA_DoesNotMove_DeadMinions_AlreadyInGraveyard()
        {
            // A minion that's already in a graveyard with 0 HP must not be
            // re-queued — the zone-is-battlefield gate excludes it. (Avoids
            // an infinite drain loop.)
            var b = BuildBoard();
            var corpse = b.eng.World.Create();
            corpse.MarkAsMinion("Corpse"); corpse.SetOwner(b.p1); corpse.SetZone(b.p1Grave);
            corpse.SetHealth(0); corpse.SetMaxHealth(5);

            var bystander = b.eng.World.Create(); bystander.MarkAsCard();
            b.eng.Enqueue(new MoveCard(bystander, b.p1Bf));

            Assert.That(corpse.ZoneId(), Is.EqualTo(b.p1Grave.Id));
        }

        [Test]
        public void DeathCascade_FireballKillsAbomination_DeathrattleKillsRaptor()
        {
            // Cascade demo: P2's Fireball at P1's Abomination.
            // - Fireball: 6 dmg → Abomination (4 HP) → -2 HP
            // - Pass 1 SBA: MoveCard(Abomination → P1 graveyard)
            // - DeathrattleBridge fires DamageAllEnemies (2 dmg to enemy hero
            //   + enemy minions on battlefield). Damages P2 hero + Raptor + Yeti.
            //   - P2 has 5 armor + ArmorRule registered → 0 net dmg
            //   - Raptor (2 HP) → 0 HP
            //   - Yeti (5 HP) → 3 HP, alive
            // - Pass 2 SBA: MoveCard(Raptor → P2 graveyard). No more deaths.
            // - Pass 3 SBA: silent. Drain quiesces.
            var b = BuildBoard();
            ArmorRule.Register(b.eng.EffectStore);   // for the armor-absorbs check

            var abomination = b.eng.World.Create();
            abomination.MarkAsMinion("Abomination");
            abomination.SetOwner(b.p1); abomination.SetZone(b.p1Bf);
            abomination.SetHealth(4); abomination.SetMaxHealth(4); abomination.SetAttack(4);
            b.eng.EffectStore.SetDirect<Deathrattle>(abomination, t => new DamageAllEnemies(t));

            var raptor = b.eng.World.Create();
            raptor.MarkAsMinion("Raptor");
            raptor.SetOwner(b.p2); raptor.SetZone(b.p2Bf);
            raptor.SetHealth(2); raptor.SetMaxHealth(2); raptor.SetAttack(3);

            var yeti = b.eng.World.Create();
            yeti.MarkAsMinion("Yeti");
            yeti.SetOwner(b.p2); yeti.SetZone(b.p2Bf);
            yeti.SetHealth(5); yeti.SetMaxHealth(5); yeti.SetAttack(4);

            b.p2.SetArmor(5);   // Warrior fantasy — drives the armor leg of the cascade

            b.eng.Stack.Push(new StackFrame(new Fireball(b.p2, abomination)));

            Assert.That(abomination.ZoneId(), Is.EqualTo(b.p1Grave.Id),
                "Abomination dies to Fireball and moves to P1's graveyard.");
            Assert.That(raptor.ZoneId(), Is.EqualTo(b.p2Grave.Id),
                "Abomination's deathrattle kills Raptor; pass 2 SBA moves it.");
            Assert.That(yeti.ZoneId(), Is.EqualTo(b.p2Bf.Id),
                "Yeti survives the deathrattle and stays on the battlefield.");
            Assert.That(yeti.Health(), Is.EqualTo(3));
            // P2's armor absorbed the deathrattle's 2 dmg entirely.
            Assert.That(b.p2.Health(), Is.EqualTo(30));
        }

        [Test]
        public void Drain_RunsOnceAfterOutermost_NotBetweenNestedCommands()
        {
            // Re-entrancy gate check: hooks inside a command's Apply
            // dispatch sub-commands which must run inline (nested under
            // the outer command's block on the wire), with SBAs draining
            // only once at the end. We assert this by counting the
            // BlockStart events: outer + inner Apply nest, MoveCard from
            // SBA appears at top level AFTER the outer block closes.
            var b = BuildBoard();
            var minion = b.eng.World.Create();
            minion.MarkAsMinion("Doomed"); minion.SetOwner(b.p1); minion.SetZone(b.p1Bf);
            minion.SetHealth(2); minion.SetMaxHealth(2);

            int checkpoint = b.eng.World.Log.Count;
            b.eng.Stack.Push(new StackFrame(new Fireball(b.p2, minion)));

            var added = b.eng.World.Log.Events.Skip(checkpoint).ToList();
            var blockNames = added.OfType<BlockStartEvent>().Select(bs => bs.Name).ToList();

            // Expected sequence:
            //   Fireball         (top-level effect)
            //     DealDamage     (inline sub-command from Fireball.Run)
            //   MoveCard         (SBA-emitted, sibling to Fireball after drain)
            Assert.That(blockNames, Is.EqualTo(new[] { "Fireball", "DealDamage", "MoveCard" }),
                "SBA-emitted MoveCard must be a sibling of the outer effect, not nested inside it.");

            // The Fireball block closes BEFORE the MoveCard block opens —
            // i.e. drain runs after the outermost inline subtree completes.
            int fireballEnd = -1, moveCardStart = -1;
            int depth = 0;
            for (int i = 0; i < added.Count; i++)
            {
                if (added[i] is BlockStartEvent bs)
                {
                    if (bs.Name == "Fireball") depth = 1;
                    else if (bs.Name == "MoveCard" && depth == 0) moveCardStart = i;
                    else if (depth > 0) depth++;
                }
                else if (added[i] is BlockEndEvent)
                {
                    if (depth > 0) depth--;
                    if (depth == 0 && fireballEnd < 0) fireballEnd = i;
                }
            }
            Assert.That(fireballEnd, Is.LessThan(moveCardStart),
                "Fireball block must close before MoveCard block opens.");
        }
    }
}
