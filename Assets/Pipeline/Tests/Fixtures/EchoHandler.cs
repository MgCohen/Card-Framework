using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class EchoHandler : Handler<Message, TestScope>
    {
        public override Type RouteType => typeof(Echo);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Message message, TestScope scope)
            => Task.FromResult<object>(((Echo)message).Value);
    }
}
