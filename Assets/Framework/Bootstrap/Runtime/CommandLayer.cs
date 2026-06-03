using Framework.Commands;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class CommandLayer : BootstrapLayer
    {
        public CommandLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
        {
            if (!_isServer) return;

            builder.Register<BlockScope>(Lifetime.Singleton);
            builder.Register<CommandHandlerRegistry>(Lifetime.Singleton);
            builder.Register<CommandService>(Lifetime.Singleton);
        }
    }
}
