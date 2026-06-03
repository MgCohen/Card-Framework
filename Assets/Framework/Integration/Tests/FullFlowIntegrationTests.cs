using System.Threading.Tasks;
using Framework.Entities;
using NUnit.Framework;

namespace Framework.Integration.Tests
{
    // End-to-end spine: one flow that touches Wire (emit/dispatch), Entities (create),
    // Tags (set/read across int + bool), and Requests (open / typed await / complete) — all
    // driven through the assembled GameEngine aggregate. Unlike the per-feature suites, this
    // proves the layers *compose*: every mutation rides the shared EventStream and is applied
    // synchronously by a live handler before the next read. It's the characterization baseline
    // the command pipeline will later let us express declaratively ("on response -> create").
    public class FullFlowIntegrationTests
    {
        [Test]
        public async Task Request_Resolution_Drives_Followup_Entity_And_Tag_Mutations()
        {
            var stack  = new EngineStack();
            var engine = stack.Engine;

            // 1. Create a player, tag it. emit EntityCreatedEvent -> handler inserts -> read back.
            var player = engine.Entities.Create(new Player());
            Assert.AreEqual(1, player.Id);
            Assert.AreEqual(new Player(), player.Type);

            engine.Tags.Set(player.Id, SampleTags.Health, 30);
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health));

            // 2. Open a request: pending until a response arrives over the wire.
            Task<DrawCardResponse> draw = engine.Requests.Open(
                new DrawCardRequest { ResponderId = player.Id, Count = 1 });

            Assert.IsFalse(draw.IsCompleted, "request stays pending until answered");
            Assert.AreEqual(1, engine.RequestStore.Open.Count);
            Assert.AreEqual(player.Id, engine.RequestStore.Get<DrawCardRequest>(1).ResponderId);

            // 3. Answer it (simulating a client->server response delivered onto the stream).
            stack.Stream.Emit(new DrawCardResponse { RequestId = 1, CardDefinitionId = 42, Cost = 3 });

            // Resolution is synchronous — the response handler completed the awaiter's TCS inline.
            var answer = await draw;
            Assert.AreEqual(0, engine.RequestStore.Open.Count, "store drops the request on answer");
            Assert.AreEqual(42, answer.CardDefinitionId);

            // 4. On resolution: create the drawn card and tag it — the "on return, do more" chain.
            var card = engine.Entities.Create(new Card(), answer.CardDefinitionId);
            Assert.AreEqual(2, card.Id, "second entity allocated after the player");
            Assert.AreEqual(new Card(), card.Type);
            Assert.AreEqual(42, card.DefinitionId);

            engine.Tags.Set(card.Id, SampleTags.Cost, answer.Cost);
            engine.Tags.Set(card.Id, SampleTags.IsCard, true);

            // 5. Final cross-store state: both entities live, tags intact across int + bool.
            Assert.AreEqual(3, engine.Tags.Get(card.Id, SampleTags.Cost));
            Assert.IsTrue(engine.Tags.Get(card.Id, SampleTags.IsCard));
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health), "player tag survives the whole flow");
            Assert.IsTrue(engine.Entities.TryGetById(player.Id, out _));
            Assert.IsTrue(engine.Entities.TryGetById(card.Id, out _));
        }
    }
}
