using System.Threading.Tasks;

namespace Framework.Pipeline.PlayTests
{
    public sealed class PingHandler : Handler<Message, Ping, PlayScope>
    {
        protected override Task<object> Handle(Ping message, PlayScope scope) => Task.FromResult<object>("pong");
    }
}
