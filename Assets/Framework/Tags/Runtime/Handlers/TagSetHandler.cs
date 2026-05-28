using System;
using System.Threading;
using System.Threading.Tasks;
using Framework.Wire;
using Scaffold.AppFlow;

namespace Framework.Tags
{
    // Mirror-side writer for TagSetEvent<T>. Depends on TagStore only — no IEventEmitter,
    // no TagService reference. The type system enforces no-bounce: this handler has no
    // path to re-emit, only to write into the store.
    internal sealed class TagSetHandler<T> : IWireEventHandler<TagSetEvent<T>>, IAsyncInitializable
        where T : unmanaged
    {
        readonly TagStore _store;
        readonly IHandlerRegistry _handlers;

        public TagSetHandler(TagStore store, IHandlerRegistry handlers)
        {
            _store = store;
            _handlers = handlers;
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            _handlers.Register<TagSetEvent<T>>(this);
            return Task.CompletedTask;
        }

        public void Apply(TagSetEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Value);

        public void Unapply(TagSetEvent<T> ev) => throw new NotImplementedException("Rollback lights up later");
    }
}
