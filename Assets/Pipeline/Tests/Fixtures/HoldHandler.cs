using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class HoldHandler : Handler<Message, TestScope>
    {
        public TaskCompletionSource<object> Release { get; } = new();

        public override Type RouteType => typeof(Hold);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Message message, TestScope scope) => Release.Task;
    }
}
