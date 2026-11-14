using Network.Domain;
using Testing;

namespace Network.Tests;

public sealed class RecordingTransportTests
{
    [Test]
    public void Sent_IsEmpty_BeforeSend()
    {
        RecordingTransport transport = new RecordingTransport();

        Assert.That(transport.Sent, Is.Empty);
    }

    [Test]
    public void Send_RecordsACopyOfEachFrame_InSendOrder()
    {
        RecordingTransport transport = new RecordingTransport();
        byte[] buffer = new byte[] { 1, 2 };

        transport.Send(buffer);
        buffer[0] = 9;
        transport.Send(buffer);

        Assert.That(transport.Sent, Is.EqualTo(new[] { new byte[] { 1, 2 }, new byte[] { 9, 2 } }));
    }

    [Test]
    public void Send_DoesNotRaiseOnReceived()
    {
        RecordingTransport transport = new RecordingTransport();
        int calls = 0;
        transport.OnReceived += _ => calls++;

        transport.Send(new byte[] { 1 });

        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void Inject_RaisesOnReceived_WithTheFrame()
    {
        RecordingTransport transport = new RecordingTransport();
        List<byte[]> received = new List<byte[]>();
        transport.OnReceived += bytes => received.Add(bytes.ToArray());

        transport.Inject(new byte[] { 3, 4 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 3, 4 } }));
        Assert.That(transport.Sent, Is.Empty);
    }

    [Test]
    public void Inject_WithNoListener_DoesNotThrow()
    {
        RecordingTransport transport = new RecordingTransport();

        Action inject = () => transport.Inject(new byte[] { 1 });

        Assert.That(inject, Throws.Nothing);
    }

    [Test]
    public void RecordingTransport_IsAnITransport()
    {
        Assert.That(new RecordingTransport(), Is.InstanceOf<ITransport>());
    }
}
