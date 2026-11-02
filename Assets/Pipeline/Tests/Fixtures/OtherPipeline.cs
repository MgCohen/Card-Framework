using Framework.RecordIds;
using Framework.Routing;

namespace Framework.Pipeline.Tests
{
    public sealed class OtherPipeline : Pipeline<Message, TestScope>
    {
        public OtherPipeline(TypeRouter<Handler<Message, TestScope>> handlers, TypeRouter<Validator<Message>> validators,
                             IdAllocator<MessageId> ids)
            : base(handlers, validators, ids)
        {
        }

        protected override TestScope CreateScope(Envelope<Message> e) => new(m => RunInline(e, m));
    }
}
