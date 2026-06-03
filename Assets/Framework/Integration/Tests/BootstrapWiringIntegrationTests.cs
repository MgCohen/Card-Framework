using System.Threading.Tasks;
using Framework.Bootstrap;
using Framework.Engine;
using Framework.Entities;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Integration.Tests
{
    // (b): the same end-to-end spine as FullFlowIntegrationTests, but driven through the real
    // VContainer composition root — the five Bootstrap layers installed in server mode, built,
    // and resolved. Where the hand-wired (a) harness proves the *types* compose, this proves the
    // *wiring* composes: DI registration, layer order, the server/client split, and the eager
    // HandlerRegistry resolve (EngineLayer's RegisterBuildCallback) that subscribes handlers to
    // the stream. A registration the layers forget would pass (a) and fail here.
    public class BootstrapWiringIntegrationTests
    {
        LifetimeScope _scope;

        [TearDown]
        public void TearDown()
        {
            if (_scope != null)
            {
                Object.DestroyImmediate(_scope.gameObject);
                _scope = null;
            }
        }

        [Test]
        public async Task Spine_Runs_Through_The_DI_Composition_Root()
        {
            _scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: true).Install(builder);
                new TagLayer(isServer: true).Install(builder);
                new EntityLayer(isServer: true).Install(builder);
                new RequestLayer(isServer: true).Install(builder);
                new StateLayer(isServer: true).Install(builder);
                new CommandLayer(isServer: true).Install(builder);

                // The request/response wire handlers the Requests feature doesn't ship yet —
                // collected by HandlerRegistry's DI list injection alongside the framework set.
                builder.RegisterHandler<DrawCardRequestHandler>();
                builder.RegisterHandler<DrawCardResponseHandler>();

                // Game-side command handlers — collected by CommandHandlerRegistry the same way.
                builder.RegisterCommandHandler<SetHealthHandler>();
                builder.RegisterCommandHandler<IntroduceCardHandler>();
            }, "IntegrationServerScope");
            if (_scope.Container == null) _scope.Build();

            var engine = _scope.Container.Resolve<GameEngine>();

            // 1. Create a player, set its health through a DI-registered command handler.
            var player = engine.Entities.Create(new Player());
            Assert.IsTrue(engine.Commands.Execute(new SetHealth { EntityId = player.Id, Value = 30 }).Accepted);
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health));

            // 2. Open a request — pending until answered.
            Task<DrawCardResponse> draw = engine.Requests.Open(
                new DrawCardRequest { ResponderId = player.Id, Count = 1 });
            Assert.IsFalse(draw.IsCompleted, "request stays pending until answered");
            Assert.AreEqual(1, engine.RequestStore.Open.Count);

            // 3. Answer it — the container-resolved response handler completes the awaiter.
            engine.Stream.Emit(new DrawCardResponse { RequestId = 1, CardDefinitionId = 42, Cost = 3 });
            var answer = await draw;
            Assert.AreEqual(0, engine.RequestStore.Open.Count, "store drops the request on answer");
            Assert.AreEqual(42, answer.CardDefinitionId);

            // 4. On resolution: create the drawn card, introduce it through a command handler.
            var card = engine.Entities.Create(new Card(), answer.CardDefinitionId);
            engine.Commands.Execute(new IntroduceCard { EntityId = card.Id, Cost = answer.Cost });

            // 5. Final cross-store state, proven over the real DI graph.
            Assert.AreEqual(3, engine.Tags.Get(card.Id, SampleTags.Cost));
            Assert.IsTrue(engine.Tags.Get(card.Id, SampleTags.IsCard));
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health), "player tag survives the whole flow");
            Assert.IsTrue(engine.Entities.TryGetById(player.Id, out _));
            Assert.IsTrue(engine.Entities.TryGetById(card.Id, out _));
        }
    }
}
