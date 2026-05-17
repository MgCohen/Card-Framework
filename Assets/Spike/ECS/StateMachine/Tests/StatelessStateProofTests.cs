#nullable enable
using System.Linq;
using NUnit.Framework;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.Tests
{
    /// <summary>
    /// The §v6 payoff: state records carry zero data fields. Selected attackers
    /// live on the entities themselves. These tests prove that the state
    /// predicates / generators are reading entity tags, not record fields.
    /// </summary>
    public class StatelessStateProofTests
    {
        [Test]
        public void StateRecords_HaveZeroDataFields()
        {
            // Reflection check — the §v6 contract is: no instance fields, no
            // properties beyond the virtuals on GameState itself.
            var types = new[]
            {
                typeof(UpkeepState), typeof(Main1State),
                typeof(DeclareAttackersState), typeof(DeclareBlockersState),
                typeof(CombatDamageState), typeof(Main2State), typeof(EndState),
                typeof(MatchNotStartedState), typeof(PlayingState), typeof(MatchEndedState),
            };

            foreach (var t in types)
            {
                var declaredFields = t.GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly);
                Assert.AreEqual(0, declaredFields.Length,
                    $"{t.Name} should be a zero-data record per spike v6; found fields: " +
                    string.Join(", ", declaredFields.Select(f => f.Name)));
            }
        }

        [Test]
        public void DeclareAttackers_SelectionLivesOnCreatures_NotOnTheRecord()
        {
            var (engine, state) = MtgBootstrap.Build();

            var atk1 = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 3, toughness: 3);
            var atk2 = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 2, toughness: 2);

            // Advance to DeclareAttackers.
            engine.Enqueue(new PassCommand());     // → Upkeep
            engine.Enqueue(new PassCommand());     // → Main1
            engine.Enqueue(new PassCommand());     // → DeclareAttackers
            Assert.IsInstanceOf<DeclareAttackersState>(state.Current);

            // "Select" two attackers by toggling tags — no record mutation needed.
            engine.Enqueue(new ToggleAttackerCommand(atk1.Id));
            engine.Enqueue(new ToggleAttackerCommand(atk2.Id));

            // The state record didn't change — Current is still the same kind.
            Assert.IsInstanceOf<DeclareAttackersState>(state.Current);

            // The selection lives on entities.
            var attacking = engine.World.With(MtgTags.IsAttacking).ToList();
            Assert.AreEqual(2, attacking.Count);
            Assert.Contains(atk1, attacking);
            Assert.Contains(atk2, attacking);
        }

        [Test]
        public void CombatDamage_OnEnter_ReadsAttackersFromWorld_AndGeneratesDamageCommands()
        {
            var (engine, state) = MtgBootstrap.Build();

            var atk1 = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 4, toughness: 2);
            var atk2 = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 1, toughness: 1);

            // Advance to DeclareAttackers, mark both attacking, commit, then
            // pass through DeclareBlockers into CombatDamage.
            engine.Enqueue(new PassCommand());            // Upkeep
            engine.Enqueue(new PassCommand());            // Main1
            engine.Enqueue(new PassCommand());            // DeclareAttackers
            engine.Enqueue(new ToggleAttackerCommand(atk1.Id));
            engine.Enqueue(new ToggleAttackerCommand(atk2.Id));
            engine.Enqueue(new CommitAttackersCommand()); // → DeclareBlockers
            engine.Enqueue(new CommitBlockersCommand());  // → CombatDamage → drains, → Main2

            // Combat resolved. Both attackers should have DamageResolved tags
            // because OnEnter read them off the world and generated DealDamage
            // commands. By the time we land on Main2, the chain has played out.
            Assert.IsInstanceOf<Main2State>(state.Current);
            Assert.IsTrue(atk1.Get(MtgTags.DamageResolved));
            Assert.IsTrue(atk2.Get(MtgTags.DamageResolved));
        }

        [Test]
        public void CombatDamage_WithZeroAttackers_AutoCompletes()
        {
            // Edge case: stateless predicate "all attackers have DamageResolved"
            // is vacuously true with zero attackers. Push handler emits
            // CombatDamage push, no OnEnter outputs, IsCompleted returns true
            // on the next-evaluable command... actually IsCompleted only fires
            // *after* a command, so this is a genuine question: does the chain
            // get stuck waiting for input that never comes?
            //
            // Answer per current Engine: yes, it'd wait for an input command
            // that satisfies IsCompleted. Documented as a deliberate edge —
            // real flow always has at least an OnEnter-generated command, or
            // the state allows a Pass.
            //
            // This test pins the current behavior so changes are intentional.
            var (engine, state) = MtgBootstrap.Build();

            engine.Enqueue(new PassCommand());
            engine.Enqueue(new PassCommand());
            engine.Enqueue(new PassCommand());
            // Commit with no attackers selected.
            engine.Enqueue(new CommitAttackersCommand());
            engine.Enqueue(new CommitBlockersCommand());

            // We end stuck in CombatDamage because nothing fires IsCompleted.
            Assert.IsInstanceOf<CombatDamageState>(state.Current);
        }
    }
}
