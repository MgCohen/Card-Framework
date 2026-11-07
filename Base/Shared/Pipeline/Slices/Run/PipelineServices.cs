namespace Pipeline.Slices.Run;

public sealed class PipelineServices
{
    private readonly Func<Type, object?> resolve;

    public PipelineServices(Func<Type, object?> resolve)
    {
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public object? Resolve(Type service)
    {
        return resolve(service);
    }

    public T Require<T>()
        where T : class
    {
        return resolve(typeof(T)) as T
            ?? throw new InvalidOperationException($"{typeof(T).FullName} is not registered.");
    }
}
