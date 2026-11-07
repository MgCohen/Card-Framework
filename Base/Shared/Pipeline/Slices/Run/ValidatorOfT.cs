using VSA.Contracts;

namespace Pipeline.Slices.Run;

public abstract class Validator<TCommand> : Validator
    where TCommand : class, IMessage
{
    public sealed override Type RouteType => typeof(TCommand);

    internal sealed override bool Dispatch(object command, out string? reason)
    {
        throw new NotImplementedException();
    }

    protected internal abstract bool Validate(TCommand command, out string? reason);
}
