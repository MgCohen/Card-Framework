using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class NestHandler : Handler<Message, TestScope>
    {
        public override Type RouteType => typeof(Nest);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override async Task<object> Handle(Message message, TestScope scope)
            => await scope.Run(new Ping());
    }
}
