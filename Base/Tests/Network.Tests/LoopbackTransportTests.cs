using Network.Domain;

namespace Network.Tests;

public sealed class LoopbackTransportTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void Send_RaisesOnReceived_OnTheOtherSide_InTheCall(bool serverSends)
    {
        Ends(serverSends, out LoopbackTransport sender, out LoopbackTransport receiver);
        List<byte[]> received = Record(receiver);

        sender.Send(new byte[] { 1, 2, 3 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 1, 2, 3 } }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Send_DoesNotRaiseOnReceived_OnTheSendingSide(bool serverSends)
    {
        Ends(serverSends, out LoopbackTransport sender, out LoopbackTransport receiver);
        List<byte[]> senderReceived = Record(sender);
        List<byte[]> receiverReceived = Record(receiver);

        sender.Send(new byte[] { 1 });

        Assert.That(senderReceived, Is.Empty);
        Assert.That(receiverReceived, Has.Count.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Send_WithNoListener_DoesNotThrow(bool serverSends)
    {
        Ends(serverSends, out LoopbackTransport sender, out LoopbackTransport _);

        Action send = () => sender.Send(new byte[] { 1 });

        Assert.That(send, Throws.Nothing);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Frames_ArriveInSendOrder(bool serverSends)
    {
        Ends(serverSends, out LoopbackTransport sender, out LoopbackTransport receiver);
        List<byte[]> received = Record(receiver);

        sender.Send(new byte[] { 1 });
        sender.Send(new byte[] { 2 });
        sender.Send(new byte[] { 3 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 } }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Send_FromAHandler_IsDeliveredInANestedCall(bool serverAnswers)
    {
        Ends(serverAnswers, out LoopbackTransport answerer, out LoopbackTransport asker);
        List<byte[]> received = Record(asker);
        int receivedInHandler = -1;
        answerer.OnReceived += bytes =>
        {
            answerer.Send(new byte[] { (byte)(bytes[0] + 1) });
            receivedInHandler = received.Count;
        };

        asker.Send(new byte[] { 7 });

        Assert.That(receivedInHandler, Is.EqualTo(1));
        Assert.That(received, Is.EqualTo(new[] { new byte[] { 8 } }));
    }

    [Test]
    public void Pairs_AreIndependent()
    {
        LoopbackTransport.Pair(out LoopbackTransport toA, out LoopbackTransport clientA);
        LoopbackTransport.Pair(out LoopbackTransport toB, out LoopbackTransport clientB);
        List<byte[]> receivedA = Record(clientA);
        List<byte[]> receivedB = Record(clientB);
        List<byte[]> receivedToB = Record(toB);

        toA.Send(new byte[] { 1 });
        clientA.Send(new byte[] { 2 });

        Assert.That(receivedA, Has.Count.EqualTo(1));
        Assert.That(receivedB, Is.Empty);
        Assert.That(receivedToB, Is.Empty);
    }

    [Test]
    public void LoopbackTransport_IsAnITransport()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport _);

        Assert.That(server, Is.InstanceOf<ITransport>());
    }

    private static void Ends(bool serverSends, out LoopbackTransport sender, out LoopbackTransport receiver)
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        sender = serverSends ? server : client;
        receiver = serverSends ? client : server;
    }

    private static List<byte[]> Record(ITransport transport)
    {
        List<byte[]> received = new List<byte[]>();
        transport.OnReceived += bytes => received.Add(bytes.ToArray());
        return received;
    }
}
