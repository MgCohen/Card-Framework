using System.Threading.Tasks;
using Framework.Pipeline;

namespace Framework.Samples.FrameworkSample
{
    public sealed class PlayCardHandler : Handler<GameMessage, PlayCard, GameScope>
    {
        protected override Task<object> Handle(PlayCard message, GameScope scope)
        {
            scope.Groups.Create(() => scope.Move(message.Card, new Zone.Board()));
            return Task.FromResult<object>(null);
        }
    }
}
