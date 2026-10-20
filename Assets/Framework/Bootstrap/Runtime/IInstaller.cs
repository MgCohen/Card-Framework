using VContainer;

namespace Framework.Bootstrap
{
    // Never an AppFlow layer: installers share their BootstrapLayer's scope.
    public interface IInstaller
    {
        void Install(IContainerBuilder builder);
    }
}
