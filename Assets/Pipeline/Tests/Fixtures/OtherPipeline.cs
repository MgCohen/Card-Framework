namespace Framework.Pipeline.Tests
{
    public sealed class OtherPipeline : Pipeline<Message, TestScope, TestView>
    {
        public OtherPipeline(PipelineServices<Message, TestScope, TestView> services) : base(services)
        {
        }

        protected override TestScope CreateScope(Envelope<Message> e) => new(m => RunInline(e, m));

        protected override TestView CreateView(Envelope<Message> e) => new();
    }
}
