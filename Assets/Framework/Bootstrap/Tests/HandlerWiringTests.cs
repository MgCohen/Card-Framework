using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap.Tests
{
    // Production-path wiring test. Fails on main because TagSetHandler<T>'s self-registration
    // ran inside IAsyncInitializable.InitializeAsync, but the open-generic registration never
    // caused that method to be invoked. After the refactor (HandlerRegistry collects handlers
    // via DI list injection + subscribes to EventStream.OnAppended in ctor) this passes — no
    // handler-side init ceremony required.
    public class HandlerWiringTests
    {
        LifetimeScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: false).Install(builder);
                new TagLayer().Install(builder);
                new EntityLayer().Install(builder);
            }, "HandlerWiringTestRoot");

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
        public void TagSetEvent_Mirrors_To_TagStore_Through_Container()
        {
            var emitter = _scope.Container.Resolve<IEventEmitter>();
            var store   = _scope.Container.Resolve<TagStore>();

            emitter.Emit(new TagSetEvent<int> { EntityId = 1, Slot = 42, Value = 30 });

            Assert.AreEqual(30, store.Get<int>(1, 42));
        }

        [Test]
        public void TagChangeEvent_Mirrors_To_TagStore_Through_Container()
        {
            var emitter = _scope.Container.Resolve<IEventEmitter>();
            var store   = _scope.Container.Resolve<TagStore>();

            emitter.Emit(new TagSetEvent<int>    { EntityId = 1, Slot = 42, Value = 30 });
            emitter.Emit(new TagChangeEvent<int> { EntityId = 1, Slot = 42, Prev = 30, Next = 25 });

            Assert.AreEqual(25, store.Get<int>(1, 42));
        }
    }
}
