using VSA.Contracts.Commands;

namespace Wire.Contracts.Commands;

// A message that a client can send to the server. The server runs it on its match bus and sends the result back to
// that client only. Its type and its pipeline decide its path; it is not a log entry.
public interface IClientMessage<TReply> : ICommand<TReply>
{
}
