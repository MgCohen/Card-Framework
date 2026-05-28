using System;
using System.Threading;
using System.Threading.Tasks;
using Framework.Wire;
using Scaffold.AppFlow;

namespace Framework.Tags
{
    internal sealed class TagRemovedHandler : IWireEventHandler<TagRemovedEvent>, IAsyncInitializable
    {
        readonly TagStore _store;
        readonly IHandlerRegistry _handlers;

        public TagRemovedHandler(TagStore store, IHandlerRegistry handlers)
        {
            _store = store;
            _handlers = handlers;
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            _handlers.Register<TagRemovedEvent>(this);
            return Task.CompletedTask;
        }

        public void Apply(TagRemovedEvent ev) => _store.Remove(ev.EntityId, ev.Slot);

        public void Unapply(TagRemovedEvent ev) => throw new NotImplementedException("Rollback lights up later");
    }
}
