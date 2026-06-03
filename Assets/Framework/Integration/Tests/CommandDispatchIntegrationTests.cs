using Framework.Bootstrap;
using Framework.Engine;
using Framework.Entities;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Integration.Tests
{
    // The command-handler analogue of BootstrapWiringIntegrationTests: a handler registered via
    // RegisterCommandHandler<T> is collected into CommandHandlerRegistry by DI and dispatched by
    // CommandService.Execute — proving the registration seam, not just the hand-wired registry.
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

        [Test]
        public void Handler_Registered_Through_DI_Is_Dispatched_On_Execute()
        {
            _scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: true).Install(builder);
                new TagLayer(isServer: true).Install(builder);
                new EntityLayer(isServer: true).Install(builder);
                new RequestLayer(isServer: true).Install(builder);
                new StateLayer(isServer: true).Install(builder);
                new CommandLayer(isServer: true).Install(builder);

                builder.RegisterCommandHandler<SetHealthCommandHandler>();
            }, "CommandDispatchScope");
            if (_scope.Container == null) _scope.Build();

            var engine = _scope.Container.Resolve<GameEngine>();
            var player = engine.Entities.Create(new Player());

            var result = engine.Commands.Execute(new SetHealthCommand { EntityId = player.Id, Value = 30 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(30, engine.Tags.Get(player.Id, SampleTags.Health),
                "the DI-registered handler ran and authored the mutation");
        }
    }
}
