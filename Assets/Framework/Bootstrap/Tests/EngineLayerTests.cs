using Framework.Engine;
using Framework.Wire;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap.Tests
{
    public class EngineLayerTests
    {
        LifetimeScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = LifetimeScope.Create(builder => new EngineLayer().Install(builder),
                                           "EngineLayerTestRoot");
            if (_scope.Container == null)
                _scope.Build();
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
        public void Resolves_GameEngine_With_Stream_And_Network()
        {
            var engine = _scope.Container.Resolve<GameEngine>();

            Assert.IsNotNull(engine);
            Assert.IsNotNull(engine.Stream);
            Assert.IsNotNull(engine.Network);
        }

        [Test]
        public void EventStream_Resolvable_As_Scoped_Interfaces()
        {
            var stream = _scope.Container.Resolve<EventStream>();
            var emitter = _scope.Container.Resolve<IEventEmitter>();
            var registry = _scope.Container.Resolve<IHandlerRegistry>();

            Assert.AreSame(stream, emitter);
            Assert.AreSame(stream, registry);
        }

        [Test]
        public void GameEngine_Stream_Is_Same_Singleton_As_Container_Resolution()
        {
            var engine = _scope.Container.Resolve<GameEngine>();
            var stream = _scope.Container.Resolve<EventStream>();

            Assert.AreSame(stream, engine.Stream);
        }
    }
}
