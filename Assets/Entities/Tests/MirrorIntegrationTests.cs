using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Entities.Tests
{
    // End-to-end mirror tests — the 4b deliverable. Server stack mutates via services;
    // events round-trip through the wire; both server and client write through the same
    // per-type handlers — handler is the sole writer on each side, no double-write.
    public class MirrorIntegrationTests
    {
        static (TestStack server, TestStack client, WireConnection w) Build()
        {
            var w = WireBridge.Connect();
            // Register the wire channels for every event we'll emit, both sides.
            w.ServerNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();
            w.ClientNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();
            w.ServerNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();
            w.ServerNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();
            w.ServerNetwork.Register<TagChangeEvent<int>, TagChangeEvent<int>.Packed>();
            w.ClientNetwork.Register<TagChangeEvent<int>, TagChangeEvent<int>.Packed>();
            w.ServerNetwork.Register<TagRemovedEvent, TagRemovedEvent.Packed>();
            w.ClientNetwork.Register<TagRemovedEvent, TagRemovedEvent.Packed>();

            var server = new TestStack(w.ServerStream);
            var client = new TestStack(w.ClientStream);

            return (server, client, w);
        }

        [Test]
        public void Create_Entity_Mirrors_To_Client_Store()
        {
            var (server, client, _) = Build();

            var serverCard = server.Entities.Create(new Card(), definitionId: 101);

            Assert.IsTrue(client.EntityStore.TryGetById(serverCard.Id, out var clientCard));
            Assert.AreEqual(new Card(), clientCard.Type);
            Assert.AreEqual(101, clientCard.DefinitionId);
        }

        [Test]
        public void TagSet_Via_Server_Mirrors_To_Client_Tag_Store()
        {
            var (server, client, _) = Build();
            var serverCard = server.Entities.Create(new Card(), definitionId: 101);

            server.Tags.Set(serverCard.Id, SampleTags.Health, 30);

            Assert.AreEqual(30, client.TagStore.Get<int>(serverCard.Id, SampleTags.Health.Slot));
        }

        [Test]
        public void TagChange_Carries_Prev_And_Next_To_Mirror()
        {
            var (server, client, _) = Build();
            var serverCard = server.Entities.Create(new Card());

            server.Tags.Set(serverCard.Id, SampleTags.Health, 30);
            server.Tags.Set(serverCard.Id, SampleTags.Health, 25);  // change

            Assert.AreEqual(25, client.TagStore.Get<int>(serverCard.Id, SampleTags.Health.Slot));
        }

        [Test]
        public void TagRemoved_Clears_Mirror_Slot()
        {
            var (server, client, _) = Build();
            var serverCard = server.Entities.Create(new Card());

            server.Tags.Set(serverCard.Id, SampleTags.Health, 30);
            Assert.IsTrue(client.TagStore.Has(serverCard.Id, SampleTags.Health.Slot));

            server.Tags.Remove(serverCard.Id, SampleTags.Health);
            Assert.IsFalse(client.TagStore.Has(serverCard.Id, SampleTags.Health.Slot));
        }

        [Test]
        public void Multiple_Entities_With_Mixed_Tags_All_Mirror()
        {
            var (server, client, _) = Build();

            var alice = server.Entities.Create(new Card(), definitionId: 101);
            var bob   = server.Entities.Create(new Card(), definitionId: 102);

            server.Tags.Set(alice.Id, SampleTags.Health, 30);
            server.Tags.Set(alice.Id, SampleTags.Attack, 5);
            server.Tags.Set(alice.Id, SampleTags.IsCard, true);
            server.Tags.Set(bob.Id,   SampleTags.Health, 10);
            server.Tags.Set(bob.Id,   SampleTags.IsCard, true);

            Assert.AreEqual(30,   client.TagStore.Get<int>(alice.Id, SampleTags.Health.Slot));
            Assert.AreEqual(5,    client.TagStore.Get<int>(alice.Id, SampleTags.Attack.Slot));
            Assert.AreEqual(true, client.TagStore.Get<bool>(alice.Id, SampleTags.IsCard.Slot));
            Assert.AreEqual(10,   client.TagStore.Get<int>(bob.Id,   SampleTags.Health.Slot));
            Assert.AreEqual(true, client.TagStore.Get<bool>(bob.Id,  SampleTags.IsCard.Slot));
        }

        [Test]
        public void Same_Value_Set_Does_Not_Round_Trip()
        {
            // Equality short-circuit: a redundant set on the server should not emit,
            // so the client should not observe a second event.
            var (server, client, w) = Build();
            var serverCard = server.Entities.Create(new Card());
            server.Tags.Set(serverCard.Id, SampleTags.Health, 30);

            var clientCountBefore = w.ClientStream.Count;

            server.Tags.Set(serverCard.Id, SampleTags.Health, 30);  // same value — no emit

            Assert.AreEqual(clientCountBefore, w.ClientStream.Count, "No new event should reach the client");
        }
    }
}
