using Framework.Commands;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    // Commands run only on the server (L1): it validates and executes intent, and only the
    // resulting mutations cross the wire. So the whole command surface — the BlockScope bracket
    // emitter, the handler registry, and the service — is server-only, matching how RequestLayer
    // / StateLayer gate their authoring services. The client reconstructs purely from mutations.
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
