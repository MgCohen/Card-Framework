using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class BoomHandler : Handler<Message, Boom, TestScope>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Boom message, TestScope scope)
            => throw new InvalidOperationException("boom");
    }
}
