using VSA.Contracts;

namespace Pipeline.Handlers.Commands;

public abstract class Validator<TCommand> : Validator
    where TCommand : class, IMessage
{
    public sealed override Type RouteType => typeof(TCommand);

    internal sealed override bool Dispatch(object command, out string? reason)
    {
        return Validate((TCommand)command, out reason);
    }

    protected internal abstract bool Validate(TCommand command, out string? reason);
}
