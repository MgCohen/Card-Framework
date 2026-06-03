using System.Collections.Generic;
using Framework.Bootstrap;
using Framework.Engine;
using Framework.Entities;
using Framework.Wire;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Integration.Tests
{
    // The command-handler analogue of BootstrapWiringIntegrationTests: stub commands registered
    // via RegisterCommandHandler<T> are collected into CommandHandlerRegistry by DI and dispatched
    // by CommandService.Execute through the real composition root — the mutations they author ride
    // the shared stream into the stores, bracketed by Block events. This is the FullFlow spine
    // expressed through commands rather than direct service calls.
    public class CommandDispatchIntegrationTests
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

        LifetimeScope BuildServerScope()
        {
            var scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: true).Install(builder);
                new TagLayer(isServer: true).Install(builder);
                new EntityLayer(isServer: true).Install(builder);
                new RequestLayer(isServer: true).Install(builder);
                new StateLayer(isServer: true).Install(builder);
                new CommandLayer(isServer: true).Install(builder);

                builder.RegisterCommandHandler<SetHealthHandler>();
                builder.RegisterCommandHandler<MarkCardHandler>();
            }, "CommandDispatchScope");
            if (scope.Container == null) scope.Build();
            return scope;
        }

        [Test]
        public void Handler_Registered_Through_DI_Is_Dispatched_On_Execute()
        {
            _scope = BuildServerScope();

            var engine = _scope.Container.Resolve<GameEngine>();
            var player = engine.Entities.Create(new Player());

            var result = engine.Commands.Execute(new SetHealth { EntityId = player.Id, Value = 30 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health),
                "the DI-registered handler ran and authored the mutation");
        }

        [Test]
        public void Commands_Drive_Mutations_In_The_Composed_Flow()
        {
            _scope = BuildServerScope();

            var engine = _scope.Container.Resolve<GameEngine>();

            var starts = new List<(int id, byte kind)>();
            var ends   = new List<(int id, byte outcome)>();
            engine.Stream.OnAppended += ev =>
            {
                if (ev is BlockStartEvent s) starts.Add((s.Id, s.Kind));
                if (ev is BlockEndEvent   e) ends.Add((e.Id, e.Outcome));
            };

            // Create two entities directly, then drive every tag mutation through registered
            // commands — two distinct command types dispatched by the registry by type.
            var player = engine.Entities.Create(new Player());
            var card   = engine.Entities.Create(new Card(), 42);

            Assert.IsTrue(engine.Commands.Execute(new SetHealth { EntityId = player.Id, Value = 30 }).Accepted);
            Assert.IsTrue(engine.Commands.Execute(new MarkCard  { EntityId = card.Id }).Accepted);

            // The command-authored mutations are applied and readable across the stores.
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health));
            Assert.IsTrue(engine.Tags.Get(card.Id, SampleTags.IsCard));

            // Each Execute bracketed its mutation with one Command block, opened and closed,
            // with monotonic ids from the shared BlockScope.
            Assert.AreEqual(2, starts.Count, "one BlockStart per command");
            Assert.IsTrue(starts.TrueForAll(b => b.kind == (byte)BlockKind.Command));
            Assert.AreEqual(new[] { 1, 2 }, starts.ConvertAll(b => b.id));
            Assert.AreEqual(2, ends.Count, "one BlockEnd per command");
            Assert.IsTrue(ends.TrueForAll(e => e.outcome == (byte)BlockOutcome.Completed));
        }
    }
}
