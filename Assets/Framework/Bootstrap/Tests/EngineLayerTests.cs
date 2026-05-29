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
    // Verifies that Engine + Tag + Entity layers compose cleanly via DI. All three layers
    // install together because GameEngine's ctor depends on EntityService + TagService.
    public class EngineLayerTests
    {
        LifetimeScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: true).Install(builder);
                new TagLayer().Install(builder);
                new EntityLayer().Install(builder);
            }, "EngineLayerTestRoot");

            if (_scope.Container == null) _scope.Build();
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

        [Test]
        public void Resolves_GameEngine_With_All_Services()
        {
            var engine = _scope.Container.Resolve<GameEngine>();

            Assert.IsNotNull(engine);
            Assert.IsNotNull(engine.Stream);
            Assert.IsNotNull(engine.Network);
            Assert.IsNotNull(engine.Entities);
            Assert.IsNotNull(engine.Tags);
        }

        [Test]
        public void EventStream_Resolvable_As_Scoped_Interfaces()
        {
            var stream  = _scope.Container.Resolve<EventStream>();
            var emitter = _scope.Container.Resolve<IEventEmitter>();

            Assert.AreSame(stream, emitter);
        }

        [Test]
        public void GameEngine_Service_Properties_Match_Container_Singletons()
        {
            var engine = _scope.Container.Resolve<GameEngine>();
            var stream = _scope.Container.Resolve<EventStream>();
            var entities = _scope.Container.Resolve<EntityService>();
            var tags = _scope.Container.Resolve<TagService>();

            Assert.AreSame(stream,   engine.Stream);
            Assert.AreSame(entities, engine.Entities);
            Assert.AreSame(tags,     engine.Tags);
        }

        [Test]
        public void EntityStore_And_TagStore_Are_Singletons()
        {
            var entityStore1 = _scope.Container.Resolve<EntityStore>();
            var entityStore2 = _scope.Container.Resolve<EntityStore>();
            var tagStore1    = _scope.Container.Resolve<TagStore>();
            var tagStore2    = _scope.Container.Resolve<TagStore>();

            Assert.AreSame(entityStore1, entityStore2);
            Assert.AreSame(tagStore1, tagStore2);
        }
    }
}
