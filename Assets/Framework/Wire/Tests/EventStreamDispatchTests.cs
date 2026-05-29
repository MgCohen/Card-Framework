using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Wire.Tests
{
    public class EventStreamDispatchTests
    {
        sealed class CountingHandler<T> : WireEventHandler<T> where T : WireEvent
        {
            public int Applied;
            public T Last;
            public override void Apply(T ev) { Applied++; Last = ev; }
        }

        static HandlerRegistry Wire(EventStream stream, params IWireEventHandler[] handlers)
            => new(stream, handlers);

        [Test]
        public void Typed_Emit_Dispatches_To_Registered_Handler()
        {
            var stream = new EventStream();
            var handler = new CountingHandler<EntityCreatedEvent>();
            Wire(stream, handler);

            stream.Emit(new EntityCreatedEvent { EntityId = 7 });

            Assert.AreEqual(1, handler.Applied);
            Assert.AreEqual(7, handler.Last.EntityId);
        }

        [Test]
        public void Typed_Emit_Does_Not_Cross_Types()
        {
            var stream = new EventStream();
            var createdH = new CountingHandler<EntityCreatedEvent>();
            var destroyedH = new CountingHandler<EntityDestroyedEvent>();
            Wire(stream, createdH, destroyedH);

            stream.Emit(new EntityCreatedEvent { EntityId = 1 });

            Assert.AreEqual(1, createdH.Applied);
            Assert.AreEqual(0, destroyedH.Applied);
        }

        [Test]
        public void Untyped_Emit_Routes_By_Runtime_Type()
        {
            var stream = new EventStream();
            var handler = new CountingHandler<EntityCreatedEvent>();
            Wire(stream, handler);

            WireEvent boxed = new EntityCreatedEvent { EntityId = 42 };
            stream.Emit(boxed);

            Assert.AreEqual(1, handler.Applied);
            Assert.AreEqual(42, handler.Last.EntityId);
        }

        [Test]
        public void Emit_Appends_To_History_And_Fires_OnAppended()
        {
            var stream = new EventStream();
            var appended = 0;
            stream.OnAppended += _ => appended++;

            stream.Emit(new EntityCreatedEvent { EntityId = 1 });
            stream.Emit(new EntityCreatedEvent { EntityId = 2 });

            Assert.AreEqual(2, stream.Count);
            Assert.AreEqual(2, appended);
            Assert.AreEqual(1, ((EntityCreatedEvent)stream[0]).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)stream[1]).EntityId);
        }

        [Test]
        public void Multiple_Handlers_Same_Type_All_Receive()
        {
            var stream = new EventStream();
            var a = new CountingHandler<EntityCreatedEvent>();
            var b = new CountingHandler<EntityCreatedEvent>();
            Wire(stream, a, b);

            stream.Emit(new EntityCreatedEvent { EntityId = 9 });

            Assert.AreEqual(1, a.Applied);
            Assert.AreEqual(1, b.Applied);
        }
    }
}
