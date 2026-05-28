using System;
using System.Threading;
using System.Threading.Tasks;
using Framework.Wire;
using Scaffold.AppFlow;

namespace Framework.Tags
{
    internal sealed class TagChangeHandler<T> : IWireEventHandler<TagChangeEvent<T>>, IAsyncInitializable
        where T : unmanaged
    {
        readonly TagStore _store;
        readonly IHandlerRegistry _handlers;

        public TagChangeHandler(TagStore store, IHandlerRegistry handlers)
        {
            _store = store;
            _handlers = handlers;
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            _handlers.Register<TagChangeEvent<T>>(this);
            return Task.CompletedTask;
        }

        public void Apply(TagChangeEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Next);

        public void Unapply(TagChangeEvent<T> ev) => throw new NotImplementedException("Rollback lights up later");
    }
}
