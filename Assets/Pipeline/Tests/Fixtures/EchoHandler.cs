using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class EchoHandler : Handler<Message, Echo, TestScope>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Echo message, TestScope scope)
            => Task.FromResult<object>(message.Value);
    }
}
