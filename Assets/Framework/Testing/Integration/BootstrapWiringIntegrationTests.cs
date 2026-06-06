using System.Threading.Tasks;
using Framework.Bootstrap;
using Framework.Engine;
using Framework.Entities;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Testing.Integration
{
    // Mechanism spine over the REAL VContainer composition root: the GameplayLayer installed in
    // server mode (composing the engine's system installers flat), built, and resolved. Where the
    // hand-wired EngineStack proves the *types* compose, this proves the *wiring* composes:
    // DI registration and collection of command handlers, command validators (including the
    // TagService-injected DealDamageValidator, constructed eagerly when GameEngine resolves — a
    // missing dependency would throw at resolve), and wire handlers; the server/client split; and
    // the request open -> answer -> resolve round-trip over the graph.
    //
    // Command *behaviour* — reject reasons, the read-modify-write 30->18 oracle, zero-wire-traffic
    // on rejection — is owned by the Acceptance pictures, which drive the same real composition
    // root through MatchHarness. This test deliberately asserts only that the parts wire and route
    // (Execute is Accepted), not what they compute.
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
                new GameplayLayer(isServer: true).Install(builder);

                // The request/response wire handlers the Requests feature doesn't ship yet —
                // collected by HandlerRegistry's DI list injection alongside the framework set.
                builder.RegisterHandler<DrawCardRequestHandler>();
                builder.RegisterHandler<DrawCardResponseHandler>();

                // Game-side command handlers — collected by CommandHandlerRegistry the same way.
                builder.RegisterCommandHandler<SetHealthHandler>();
                builder.RegisterCommandHandler<DealDamageHandler>();
                builder.RegisterCommandHandler<IntroduceCardHandler>();

                // Game-side command validators — collected by CommandValidatorRegistry the same way.
                // DealDamageValidator ctor-injects TagService: resolving GameEngine forces the
                // validator array to build, so a missing dependency would throw here — proving
                // stateful validators wire through the container even though no DealDamage runs.
                builder.RegisterCommandValidator<SetHealthValidator>();
                builder.RegisterCommandValidator<DealDamageValidator>();
            }, "IntegrationServerScope");
            if (_scope.Container == null) _scope.Build();

            var engine = _scope.Container.Resolve<GameEngine>();

            // 1. A DI-collected command handler is found and routed: SetHealth executes and is
            //    accepted through the container-composed engine. (The resulting value is an
            //    Acceptance oracle, not asserted here.)
            var player = engine.Entities.Create(new Player());
            Assert.IsTrue(engine.Commands.Execute(new SetHealth { EntityId = player.Id, Value = 30 }).Accepted);

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

            // 4. On resolution: create the drawn card and introduce it through another DI-collected
            //    command handler — accepted and routed the same way.
            var card = engine.Entities.Create(new Card(), answer.CardDefinitionId);
            Assert.IsTrue(engine.Commands.Execute(new IntroduceCard { EntityId = card.Id, Cost = answer.Cost }).Accepted);

            // 5. Both entities resolve and survive the request-driven flow over the real DI graph.
            Assert.IsTrue(engine.Entities.TryGetById(player.Id, out _));
            Assert.IsTrue(engine.Entities.TryGetById(card.Id, out _));
        }
    }
}
