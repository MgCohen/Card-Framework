using VSA.Contracts;

namespace Peers.Contracts.Commands;

public static class PeerErrors
{
    public static readonly Error HandlerFailed = new Error(
        "Peers.HandlerFailed", "The server handler of the client message threw an exception.");
}
