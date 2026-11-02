using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class PingHandler : Handler<Message, TestScope>
    {
        public override Type RouteType => typeof(Ping);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Message message, TestScope scope) => Task.FromResult<object>("pong");
    }
}
