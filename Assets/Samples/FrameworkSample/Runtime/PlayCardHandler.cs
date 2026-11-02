using System;
using System.Threading.Tasks;
using Framework.Pipeline;

namespace Framework.Samples.FrameworkSample
{
    public sealed class PlayCardHandler : Handler<GameMessage, GameScope>
    {
        public override Type RouteType => typeof(PlayCard);

        protected override Task<object> Handle(GameMessage message, GameScope scope)
        {
            var play = (PlayCard)message;
            scope.Groups.Create(() => scope.Move(play.Card, new Zone.Board()));
            return Task.FromResult<object>(null);
        }
    }
}
