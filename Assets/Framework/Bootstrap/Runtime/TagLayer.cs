using Framework.Tags;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
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
