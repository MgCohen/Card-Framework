using System.Reflection;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Handlers.Commands;

public sealed class PipelineCommandSender : ICommandSender
{
    private readonly PipelineServices services;
    private readonly PipelineLayer[] layers;

    public PipelineCommandSender(PipelineServices services, IEnumerable<PipelineLayer> layers)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        this.layers = layers.ToArray();
        HashSet<Type> seen = new HashSet<Type>();
        foreach (PipelineLayer layer in this.layers)
        {
            if (!seen.Add(layer.MessageType))
            {
                throw new InvalidOperationException(
                    $"Two layers have the message type {layer.MessageType.Name}. A message type has one layer.");
            }
        }
    }

    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        PipelineLayer? layer = LayerOf(command.GetType());
        return layer == null ? SendPlainAsync(command, ct) : RunAsync(layer, command, ct);
    }

    private async Task<TResult> RunAsync<TResult>(PipelineLayer layer, ICommand<TResult> command, CancellationToken ct)
    {
        Envelope envelope = await layer.RunMessageAsync(command, services, ct);
        if (envelope.State == EnvelopeState.Rejected)
        {
            throw new CommandRejectedException(envelope);
        }

        return Result.From<TResult>(envelope).Value!;
    }

    private Task<TResult> SendPlainAsync<TResult>(ICommand<TResult> command, CancellationToken ct)
    {
        Type type = command.GetType();
        Type service = typeof(CommandHandler<,>).MakeGenericType(type, typeof(TResult));
        object handler = services.Resolve(service)
            ?? throw new InvalidOperationException(
                $"Command {type.FullName} has no handler. Add a CommandHandler<{type.Name}, {typeof(TResult).Name}>.");
        if (handler is IPipelineHandler)
        {
            throw new InvalidOperationException(
                $"{handler.GetType().Name} is a pipeline handler, but no layer runs {type.Name}. Register its layer.");
        }

        MethodInfo handle = service.GetMethod("HandleAsync", new[] { type, typeof(CancellationToken) })!;
        object[] arguments = { command, ct };
        return (Task<TResult>)handle.Invoke(handler, BindingFlags.DoNotWrapExceptions, null, arguments, null)!;
    }

    private PipelineLayer? LayerOf(Type message)
    {
        PipelineLayer[] matches = layers.Where(layer => layer.MessageType.IsAssignableFrom(message)).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"{message.Name} is a message of two layers: "
                + $"{matches[0].MessageType.Name} and {matches[1].MessageType.Name}.");
        }

        return matches.Length == 1 ? matches[0] : null;
    }
}
