using System.Threading.Tasks;

namespace Framework.Pipeline.PlayTests
{
    public sealed class NestHandler : Handler<Message, Nest, PlayScope>
    {
        protected override async Task<object> Handle(Nest message, PlayScope scope) => await scope.Run(new Ping());
    }
}
