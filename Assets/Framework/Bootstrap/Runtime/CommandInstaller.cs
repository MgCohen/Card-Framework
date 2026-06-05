using Framework.Commands;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class CommandInstaller : IInstaller
    {
        readonly bool _isServer;

        public CommandInstaller(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            if (!_isServer) return;

            builder.Register<BlockScope>(Lifetime.Singleton);
            builder.Register<CommandHandlerRegistry>(Lifetime.Singleton);
            builder.Register<CommandValidatorRegistry>(Lifetime.Singleton);
            builder.Register<CommandService>(Lifetime.Singleton);
        }
    }
}
