using Framework.Tags;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // Tag storage + command surface + apply handlers. Per-T tag value types are registered
    // via RegisterTagValueType<T> — one line per T that any TagKey<T> in the game uses.
    public sealed class TagLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<TagStore>(Lifetime.Singleton);
            builder.Register<TagService>(Lifetime.Singleton);

            builder.RegisterHandler<TagRemovedHandler>();
            builder.RegisterTagValueType<int>();
            builder.RegisterTagValueType<bool>();
        }
    }
}
