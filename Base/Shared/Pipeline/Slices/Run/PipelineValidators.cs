namespace Pipeline.Slices.Run;

public sealed class PipelineValidators
{
    public PipelineValidators(IEnumerable<Validator> validators)
    {
        throw new NotImplementedException();
    }

    internal static IReadOnlyList<Type> Find(bool includeTests)
    {
        throw new NotImplementedException();
    }

    internal bool Validate(object command, out string? reason)
    {
        throw new NotImplementedException();
    }
}
