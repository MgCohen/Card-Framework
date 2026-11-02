using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class SpawnHandler : Handler<Message, Spawn, TestScope>
    {
        public TestPipeline Target { get; set; }

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Spawn message, TestScope scope)
            => Task.FromResult<object>(Target.Add(new Ping()));
    }
}
