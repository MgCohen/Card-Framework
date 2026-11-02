using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public class FrameTests
    {
        static (CaptureTransport transport, NetworkChannel<PingEvent> channel) Build()
        {
            var transport = new CaptureTransport();
            var network = new Network(transport);
            var channel = network.Register<PingEvent, PingEvent.Packed>();
            return (transport, channel);
        }

        [Test]
        public void Send_WritesIdThenPackedStruct()
        {
            var (transport, channel) = Build();

            channel.Send(new PingEvent { Value = 42 });

            var frame = transport.Sent[0];
            Assert.AreEqual(0, frame[0]);
            Assert.AreEqual(1 + Marshal.SizeOf<PingEvent.Packed>(), frame.Length);
            var expected = ((IPackable<PingEvent.Packed>)new PingEvent { Value = 42 }).PackTyped(null);
            Assert.AreEqual(expected, MemoryMarshal.Read<PingEvent.Packed>(frame.AsSpan(1)));
        }

        [Test]
        public void Receive_SentFrame_RaisesTheEvent()
        {
            var (transport, channel) = Build();
            PingEvent received = null;
            channel.OnReceived += ev => received = ev;

            channel.Send(new PingEvent { Value = 42 });
            transport.Inject(transport.Sent[0]);

            Assert.AreEqual(42, received.Value);
        }

        [Test]
        public void Receive_EmptyFrame_Throws()
        {
            var (transport, _) = Build();
            Assert.Throws<InvalidOperationException>(() => transport.Inject(new byte[0]));
        }

        [Test]
        public void Receive_UnknownId_Throws()
        {
            var (transport, _) = Build();
            Assert.Throws<InvalidOperationException>(() => transport.Inject(new byte[] { 9 }));
        }

        [Test]
        public void Receive_ShortPayload_Throws()
        {
            var (transport, _) = Build();
            Assert.Throws<InvalidOperationException>(() => transport.Inject(new byte[] { 0, 1 }));
        }

        [Test]
        public void Receive_LongPayload_Throws()
        {
            var (transport, channel) = Build();
            channel.Send(new PingEvent { Value = 42 });
            var frame = new byte[transport.Sent[0].Length + 1];
            transport.Sent[0].CopyTo(frame, 0);

            Assert.Throws<InvalidOperationException>(() => transport.Inject(frame));
        }
    }
}
