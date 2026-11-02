using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.PlayTests
{
    public sealed class PingHandler : Handler<Message, PlayScope>
    {
        public override Type RouteType => typeof(Ping);

        protected override Task<object> Handle(Message message, PlayScope scope) => Task.FromResult<object>("pong");
    }
}
