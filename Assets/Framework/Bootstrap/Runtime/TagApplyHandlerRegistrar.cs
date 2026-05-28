using System.Threading;
using System.Threading.Tasks;
using Framework.Tags;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // Drives closed-type instantiation of TagSetHandler<T> / TagChangeHandler<T> for every
    // TagKey<T> in the registry. Visitor pattern + open-generic VContainer resolution =
    // zero reflection in our code; the container does its own MakeGenericType under the hood.
    //
    // Each resolved handler's ctor self-registers via IAsyncInitializable (called by the
    // AppFlow layer-init runner after this registrar's InitializeAsync completes).
    internal sealed class TagApplyHandlerRegistrar : ITagKeyVisitor, IAsyncInitializable
    {
        readonly IObjectResolver _resolver;

        public TagApplyHandlerRegistrar(IObjectResolver resolver) { _resolver = resolver; }

        public Task InitializeAsync(CancellationToken ct)
        {
            foreach (var key in TagRegistry.All) key.Accept(this);
            return Task.CompletedTask;
        }

        public void Visit<T>(TagKey<T> key) where T : unmanaged
        {
            _resolver.Resolve<TagSetHandler<T>>();
            _resolver.Resolve<TagChangeHandler<T>>();
        }
    }
}
