using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.RecordIds;
using Framework.TypeRouting;

namespace Framework.Pipeline.PlayTests
{
    public sealed class PlayPipeline : Pipeline<Message, PlayScope, PlayView>
    {
        public PlayPipeline(params Handler<Message, PlayScope>[] handlers)
            : base(new PipelineServices<Message, PlayScope, PlayView>(
                new TypeRouter<Handler<Message, PlayScope>>(handlers, RouteMode.Single),
                new TypeRouter<Validator<Message, PlayView>>(new List<Validator<Message, PlayView>>(), RouteMode.Single),
                new IdAllocator<MessageId>()))
        {
        }

        public Task<Envelope<Message>> RunInlineForTest(Message message)
        {
            var parent = Add(new Ping());
            return RunInline(parent, message);
        }

        protected override PlayScope CreateScope(Envelope<Message> e) => new(m => RunInline(e, m));

        protected override PlayView CreateView(Envelope<Message> e) => new();
    }
}
