using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class BoomHandler : Handler<Message, TestScope>
    {
        public override Type RouteType => typeof(Boom);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Message message, TestScope scope)
            => throw new InvalidOperationException("boom");
    }
}
