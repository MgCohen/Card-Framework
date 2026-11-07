namespace Pipeline.Slices.Run;

public sealed class PipelineServices
{
    public PipelineServices(Func<Type, object?> resolve)
    {
        throw new NotImplementedException();
    }

    public object? Resolve(Type service)
    {
        throw new NotImplementedException();
    }

    public T Require<T>()
        where T : class
    {
        throw new NotImplementedException();
    }
}
