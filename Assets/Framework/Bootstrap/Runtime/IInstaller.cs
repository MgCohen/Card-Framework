using VContainer;

namespace Framework.Bootstrap
{
    // A single system's DI registrations. Installers are composed flat by a BootstrapLayer
    // into one scope; they are not AppFlow layers and never get a scope of their own.
    public interface IInstaller
    {
        void Install(IContainerBuilder builder);
    }
}
