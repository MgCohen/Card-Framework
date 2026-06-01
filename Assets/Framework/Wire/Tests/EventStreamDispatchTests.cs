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
            var handler = new CountingHandler<PingEvent>();
            Wire(stream, handler);

            stream.Emit(new PingEvent { Value = 7 });

            Assert.AreEqual(1, handler.Applied);
            Assert.AreEqual(7, handler.Last.Value);
        }

        [Test]
        public void Typed_Emit_Does_Not_Cross_Types()
        {
            var stream = new EventStream();
            var pingH = new CountingHandler<PingEvent>();
            var pongH = new CountingHandler<PongEvent>();
            Wire(stream, pingH, pongH);

            stream.Emit(new PingEvent { Value = 1 });

            Assert.AreEqual(1, pingH.Applied);
            Assert.AreEqual(0, pongH.Applied);
        }

        [Test]
        public void Untyped_Emit_Routes_By_Runtime_Type()
        {
            var stream = new EventStream();
            var handler = new CountingHandler<PingEvent>();
            Wire(stream, handler);

            WireEvent boxed = new PingEvent { Value = 42 };
            stream.Emit(boxed);

            Assert.AreEqual(1, handler.Applied);
            Assert.AreEqual(42, handler.Last.Value);
        }

        [Test]
        public void Emit_Appends_To_History_And_Fires_OnAppended()
        {
            var stream = new EventStream();
            var appended = 0;
            stream.OnAppended += _ => appended++;

            stream.Emit(new PingEvent { Value = 1 });
            stream.Emit(new PingEvent { Value = 2 });

            Assert.AreEqual(2, stream.Count);
            Assert.AreEqual(2, appended);
            Assert.AreEqual(1, ((PingEvent)stream[0]).Value);
            Assert.AreEqual(2, ((PingEvent)stream[1]).Value);
        }

        [Test]
        public void Multiple_Handlers_Same_Type_All_Receive()
        {
            var stream = new EventStream();
            var a = new CountingHandler<PingEvent>();
            var b = new CountingHandler<PingEvent>();
            Wire(stream, a, b);

            stream.Emit(new PingEvent { Value = 9 });

            Assert.AreEqual(1, a.Applied);
            Assert.AreEqual(1, b.Applied);
        }
    }
}
