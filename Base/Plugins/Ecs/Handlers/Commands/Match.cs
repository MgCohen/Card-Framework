using Ecs.Domain;
using VSA.Contracts.Commands;

namespace Ecs.Handlers.Commands;

public sealed class Match : IDisposable
{
    public Match(IServiceProvider app)
    {
        throw new NotImplementedException();
    }

    public IServiceProvider Services => throw new NotImplementedException();

    public ICommandSender Commands => throw new NotImplementedException();

    public IWorldReader Reader => throw new NotImplementedException();

    internal World World => throw new NotImplementedException();

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
