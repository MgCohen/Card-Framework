using Framework.Tags;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // Tag storage + apply handlers register on both sides. TagService — the command surface —
    // is server-only: client mirrors don't author tags, they read the store. RegisterTagValueType<T>
    // wires the Set + Change handlers for that T; one call per T (int, bool, ...) the game uses.
    public sealed class TagLayer : IScopeLayer
    {
        readonly bool _isServer;

        public TagLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<TagStore>(Lifetime.Singleton);
            builder.Register<TagRegistry>(Lifetime.Singleton);
            if (_isServer) builder.Register<TagService>(Lifetime.Singleton);

            builder.RegisterHandler<TagRemovedHandler>();
            builder.RegisterTagValueType<int>();
            builder.RegisterTagValueType<bool>();
        }
    }
}
