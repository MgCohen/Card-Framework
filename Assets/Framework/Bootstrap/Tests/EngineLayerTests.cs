using Framework.Engine;
using Framework.Entities;
using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap.Tests
{
    // Verifies that Engine + Tag + Entity layers compose cleanly via DI on each side.
    // Server scope resolves GameEngine + command services + stores. Client scope resolves
    // only the substrate (EventStream, stores, handlers) — no engine, no services. The
    // "client cannot author state" rule is enforced structurally by what's registered.
    public class EngineLayerTests
    {
        LifetimeScope _scope;

        LifetimeScope BuildScope(bool isServer, string name)
        {
            var scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer).Install(builder);
                new TagLayer(isServer).Install(builder);
                new EntityLayer(isServer).Install(builder);
            }, name);
            if (scope.Container == null) scope.Build();
            return scope;
        }

        [TearDown]
        public void TearDown()
        {
            if (_scope != null)
            {
                Object.DestroyImmediate(_scope.gameObject);
                _scope = null;
            }
        }

        // --- Server scope -----------------------------------------------------

        [Test]
        public void Server_Resolves_GameEngine_With_All_Services_And_Stores()
        {
            _scope = BuildScope(isServer: true, "ServerScope");

            var engine = _scope.Container.Resolve<GameEngine>();

            Assert.IsNotNull(engine);
            Assert.IsNotNull(engine.Stream);
            Assert.IsNotNull(engine.Network);
            Assert.IsNotNull(engine.EntityStore);
            Assert.IsNotNull(engine.TagStore);
            Assert.IsNotNull(engine.Entities);
            Assert.IsNotNull(engine.Tags);
        }

        [Test]
        public void Server_GameEngine_Properties_Match_Container_Singletons()
        {
            _scope = BuildScope(isServer: true, "ServerScope");

            var engine   = _scope.Container.Resolve<GameEngine>();
            var stream   = _scope.Container.Resolve<EventStream>();
            var entities = _scope.Container.Resolve<EntityService>();
            var tags     = _scope.Container.Resolve<TagService>();

            Assert.AreSame(stream,   engine.Stream);
            Assert.AreSame(entities, engine.Entities);
            Assert.AreSame(tags,     engine.Tags);
        }

        [Test]
        public void Server_EntityStore_And_TagStore_Are_Singletons()
        {
            _scope = BuildScope(isServer: true, "ServerScope");

            Assert.AreSame(_scope.Container.Resolve<EntityStore>(), _scope.Container.Resolve<EntityStore>());
            Assert.AreSame(_scope.Container.Resolve<TagStore>(),    _scope.Container.Resolve<TagStore>());
        }

        // --- Client scope -----------------------------------------------------

        [Test]
        public void Client_Resolves_Substrate_Directly()
        {
            _scope = BuildScope(isServer: false, "ClientScope");

            // Pieces the client actually needs — each injectable on its own; no engine handle.
            Assert.IsNotNull(_scope.Container.Resolve<EventStream>());
            Assert.IsNotNull(_scope.Container.Resolve<Network>());
            Assert.IsNotNull(_scope.Container.Resolve<EntityStore>());
            Assert.IsNotNull(_scope.Container.Resolve<TagStore>());
        }

        [Test]
        public void Client_Does_Not_Register_Command_Services()
        {
            _scope = BuildScope(isServer: false, "ClientScope");

            Assert.Throws<VContainerException>(() => _scope.Container.Resolve<EntityService>());
            Assert.Throws<VContainerException>(() => _scope.Container.Resolve<TagService>());
        }

        [Test]
        public void Client_Does_Not_Register_GameEngine()
        {
            _scope = BuildScope(isServer: false, "ClientScope");

            Assert.Throws<VContainerException>(() => _scope.Container.Resolve<GameEngine>());
        }

        // --- Shared substrate -------------------------------------------------

        [Test]
        public void EventStream_Resolvable_As_Scoped_Emitter_Interface()
        {
            _scope = BuildScope(isServer: true, "ServerScope");

            var stream  = _scope.Container.Resolve<EventStream>();
            var emitter = _scope.Container.Resolve<IEventEmitter>();

            Assert.AreSame(stream, emitter);
        }
    }
}
