namespace Pipeline.Handlers.Commands;

public abstract class Validator : IRouted
{
    public abstract Type RouteType { get; }

    internal abstract bool Dispatch(object command, out string? reason);
}
