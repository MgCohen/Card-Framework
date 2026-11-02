using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.PlayTests
{
    public sealed class NestHandler : Handler<Message, PlayScope>
    {
        public override Type RouteType => typeof(Nest);

        protected override async Task<object> Handle(Message message, PlayScope scope) => await scope.Run(new Ping());
    }
}
