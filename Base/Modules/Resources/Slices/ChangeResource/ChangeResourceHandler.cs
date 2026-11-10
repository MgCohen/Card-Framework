using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Resources.Slices.ChangeResource;

public sealed class ChangeResourceHandler : CommandHandler<ChangeResourceCommand, int>
{
    private readonly ICommandSender commands;

    public ChangeResourceHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override Task<Result<int>> HandleAsync(Envelope<ChangeResourceCommand> envelope)
    {
        throw new NotImplementedException();
    }
}
