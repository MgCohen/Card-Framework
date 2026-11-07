using VSA.Contracts.Commands;

namespace Pipeline.Slices.Run;

public sealed class PipelineCommandSender : ICommandSender
{
    public PipelineCommandSender(PipelineServices services, IEnumerable<PipelineLayer> layers)
    {
        throw new NotImplementedException();
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
