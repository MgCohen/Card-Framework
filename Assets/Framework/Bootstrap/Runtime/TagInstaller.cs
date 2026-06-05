using Framework.Tags;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class TagInstaller : IInstaller
    {
        readonly bool _isServer;

        public TagInstaller(bool isServer) { _isServer = isServer; }

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
