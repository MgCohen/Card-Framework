using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class HoldHandler : Handler<Message, Hold, TestScope>
    {
        public TaskCompletionSource<object> Release { get; } = new();

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Hold message, TestScope scope) => Release.Task;
    }
}
