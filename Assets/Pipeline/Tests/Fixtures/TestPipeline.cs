using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.RecordIds;
using Framework.TypeRouting;
using VContainer;

namespace Framework.Pipeline.Tests
{
    public sealed class TestPipeline : Pipeline<Message, TestScope, TestView>
    {
        [Inject]
        public TestPipeline(PipelineServices<Message, TestScope, TestView> services) : base(services)
        {
        }

        public TestPipeline(params object[] members)
            : base(new PipelineServices<Message, TestScope, TestView>(
                new TypeRouter<Handler<Message, TestScope>>(Of<Handler<Message, TestScope>>(members), RouteMode.Single),
                new TypeRouter<Validator<Message, TestView>>(Of<Validator<Message, TestView>>(members), RouteMode.Single),
                new IdAllocator<MessageId>()))
        {
        }

        public TestView View { get; set; } = new();

        public bool Lifo { get; set; }
        public bool ScopeThrows { get; set; }
        public bool ScopeIsNull { get; set; }
        public List<string> Steps { get; } = new();
        public List<TestScope> Scopes { get; } = new();

        static List<T> Of<T>(object[] members)
        {
            var list = new List<T>();
            foreach (var m in members)
                if (m is T t) list.Add(t);
            return list;
        }

        protected override Envelope<Message> SelectNext(IReadOnlyList<Envelope<Message>> items)
            => Lifo && items.Count > 0 ? items[items.Count - 1] : base.SelectNext(items);

        protected override TestScope CreateScope(Envelope<Message> e)
        {
            Steps.Add("scope");
            if (ScopeThrows) throw new InvalidOperationException("scope");
            if (ScopeIsNull) return null;
            var scope = new TestScope(m => RunInline(e, m));
            Scopes.Add(scope);
            return scope;
        }

        protected override TestView CreateView(Envelope<Message> e) => View;

        protected override bool Validate(Envelope<Message> e, out string reason)
        {
            Steps.Add("validate");
            return base.Validate(e, out reason);
        }

        protected override Task Before(Envelope<Message> e, TestScope scope)
        {
            Steps.Add("before");
            return base.Before(e, scope);
        }

        protected override Task<object> Handle(Envelope<Message> e, TestScope scope)
        {
            Steps.Add("handle");
            return base.Handle(e, scope);
        }

        protected override Task After(Envelope<Message> e, TestScope scope)
        {
            Steps.Add("after");
            return base.After(e, scope);
        }
    }
}
