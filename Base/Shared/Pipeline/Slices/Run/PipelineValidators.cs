namespace Pipeline.Slices.Run;

public sealed class PipelineValidators
{
    private readonly TypeRouter<Validator> router;

    public PipelineValidators(IEnumerable<Validator> validators)
    {
        router = new TypeRouter<Validator>(validators.ToArray(), RouteMode.Single);
    }

    internal static IReadOnlyList<Type> Find(bool includeTests)
    {
        return TypeScan.ConcreteTypesOf(typeof(Validator), includeTests);
    }

    internal bool Validate(object command, out string? reason)
    {
        if (!router.TryGet(command.GetType(), out Validator? validator))
        {
            reason = null;
            return true;
        }

        return validator!.Dispatch(command, out reason);
    }
}
