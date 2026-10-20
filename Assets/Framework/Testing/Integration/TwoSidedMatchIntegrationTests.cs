using Framework.Bootstrap;
using Framework.Entities;
using Framework.States;
using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using Tools.Rulebook;
using VContainer;

namespace Framework.Testing.Integration
{
    // Server and client GameplayLayer joined by a loopback transport: the server runs a
    // command, and the client must end with the same stores from the same events.
    [Rule("TEST-02")]
    public class TwoSidedMatchIntegrationTests
    {
        static MatchPair StartWithDamageCommands() => MatchPair.Start((builder, isServer) =>
        {
            if (!isServer) return;
            builder.RegisterCommandHandler<SetHealthHandler>();
            builder.RegisterCommandHandler<DealDamageHandler>();
            builder.RegisterCommandValidator<DealDamageValidator>();
        });

        [Test]
        public void Client_Has_Stores_But_No_Engine()
        {
            using var match = StartWithDamageCommands();

            Assert.IsNotNull(match.Client.Resolve<TagStore>());
            Assert.Throws<VContainerException>(() => match.Client.Resolve<TagService>());
        }

        [Test]
        public void Command_On_Server_Converges_On_Client()
        {
            using var match = StartWithDamageCommands();

            var unit = match.Engine.Entities.Create(new Card());
            Assert.IsTrue(match.Execute(new SetHealth { EntityId = unit.Id, Value = 30 }).Accepted);
            Assert.IsTrue(match.Execute(new DealDamage { EntityId = unit.Id, Amount = 12 }).Accepted);

            var clientTags     = match.Client.Resolve<TagStore>();
            var clientEntities = match.Client.Resolve<EntityStore>();
            Assert.IsTrue(clientEntities.TryGetById(unit.Id, out _), "client knows the entity");
            Assert.AreEqual(18, clientTags.Get<int>(unit.Id, SampleTags.Health.Slot));
            Assert.AreEqual(match.Engine.TagStore.Get<int>(unit.Id, SampleTags.Health.Slot),
                            clientTags.Get<int>(unit.Id, SampleTags.Health.Slot));
            match.AssertStreamsMatch();
        }

        [Test]
        public void Rejected_Command_Sends_Nothing_To_Client()
        {
            using var match = StartWithDamageCommands();

            var unit = match.Engine.Entities.Create(new Card());
            var before = match.Client.Resolve<EventStream>().Count;

            Assert.IsFalse(match.Execute(new DealDamage { EntityId = unit.Id, Amount = 5 }).Accepted);

            Assert.AreEqual(before, match.Client.Resolve<EventStream>().Count);
            match.AssertStreamsMatch();
        }

        [Test]
        public void State_Push_Converges_On_Client()
        {
            using var match = StartWithDamageCommands();

            match.Engine.States.Push<MatchEndedState>();

            Assert.IsInstanceOf<MatchEndedState>(match.Client.Resolve<StateStore>().Current);
            match.AssertStreamsMatch();
        }
    }
}
