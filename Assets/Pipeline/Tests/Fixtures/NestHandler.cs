using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class NestHandler : Handler<Message, Nest, TestScope>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override async Task<object> Handle(Nest message, TestScope scope)
            => await scope.Run(new Ping());
    }
}
