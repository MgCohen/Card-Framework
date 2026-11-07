namespace Pipeline.Slices.Run;

public sealed class PipelineRuns
{
    private readonly AsyncLocal<Envelope?> current = new AsyncLocal<Envelope?>();

    internal Envelope? Current
    {
        get => current.Value;
        set => current.Value = value;
    }
}
