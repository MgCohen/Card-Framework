using VContainer;

namespace Framework.Bootstrap
{
    // One AppFlow layer = one altitude of the app (gameplay, metagame, navigation, ...).
    // The gameplay layer composes the engine's systems as flat installers into a single
    // scope, so GameEngine and every service/store it aggregates resolve from one container.
    public sealed class GameplayLayer : BootstrapLayer
    {
        public GameplayLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
        {
            new EngineInstaller(_isServer).Install(builder);
            new TagInstaller(_isServer).Install(builder);
            new EntityInstaller(_isServer).Install(builder);
            new RequestInstaller(_isServer).Install(builder);
            new StateInstaller(_isServer).Install(builder);
            new CommandInstaller(_isServer).Install(builder);
        }
    }
}
