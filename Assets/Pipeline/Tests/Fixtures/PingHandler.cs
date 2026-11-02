using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class PingHandler : Handler<Message, Ping, TestScope>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Ping message, TestScope scope) => Task.FromResult<object>("pong");
    }
}
