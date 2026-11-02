using System;
using System.Threading.Tasks;

namespace Framework.Pipeline.Tests
{
    public sealed class SpawnHandler : Handler<Message, TestScope>
    {
        public TestPipeline Target { get; set; }

        public override Type RouteType => typeof(Spawn);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override Task<object> Handle(Message message, TestScope scope)
            => Task.FromResult<object>(Target.Add(new Ping()));
    }
}
