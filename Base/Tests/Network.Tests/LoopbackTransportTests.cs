using Network.Domain;

namespace Network.Tests;

public sealed class LoopbackTransportTests
{
    [Test]
    public void Send_OnServerSide_RaisesOnReceived_OnClientSide_InTheCall()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        List<byte[]> received = Record(client);

        server.Send(new byte[] { 1, 2, 3 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 1, 2, 3 } }));
    }

    [Test]
    public void Send_OnClientSide_RaisesOnReceived_OnServerSide_InTheCall()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        List<byte[]> received = Record(server);

        client.Send(new byte[] { 4, 5 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 4, 5 } }));
    }

    [Test]
    public void Send_DoesNotRaiseOnReceived_OnTheSendingSide()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        List<byte[]> serverReceived = Record(server);
        List<byte[]> clientReceived = Record(client);

        server.Send(new byte[] { 1 });

        Assert.That(serverReceived, Is.Empty);
        Assert.That(clientReceived, Has.Count.EqualTo(1));
    }

    [Test]
    public void Send_WithNoListener_DoesNotThrow()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport _);

        Action send = () => server.Send(new byte[] { 1 });

        Assert.That(send, Throws.Nothing);
    }

    [Test]
    public void Frames_ArriveInSendOrder()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        List<byte[]> received = Record(client);

        server.Send(new byte[] { 1 });
        server.Send(new byte[] { 2 });
        server.Send(new byte[] { 3 });

        Assert.That(received, Is.EqualTo(new[] { new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 } }));
    }

    [Test]
    public void Send_FromAHandler_IsDeliveredInANestedCall()
    {
        LoopbackTransport.Pair(out LoopbackTransport server, out LoopbackTransport client);
        server.OnReceived += bytes => server.Send(new byte[] { (byte)(bytes[0] + 1) });
        List<byte[]> received = Record(client);

        client.Send(new byte[] { 7 });

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

    private static List<byte[]> Record(ITransport transport)
    {
        List<byte[]> received = new List<byte[]>();
        transport.OnReceived += bytes => received.Add(bytes.ToArray());
        return received;
    }
}
