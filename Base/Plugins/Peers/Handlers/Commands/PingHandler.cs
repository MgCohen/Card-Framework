using Peers.Contracts.Commands;
using VSA.Contracts;
using VSA.Handlers.Commands;

namespace Peers.Handlers.Commands;

public sealed class PingHandler : CommandHandler<Ping, Pong>
{
    public override Task<Result<Pong>> HandleAsync(Envelope<Ping> envelope)
    {
        return Task.FromResult<Result<Pong>>(new Pong(envelope.Message.Sequence));
    }
}
