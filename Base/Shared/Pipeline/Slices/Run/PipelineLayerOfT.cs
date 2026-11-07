using Pipeline.Domain.Shared;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Slices.Run;

public sealed class PipelineLayer<TMessage, TScope> : PipelineLayer
    where TMessage : class, IMessage
    where TScope : PipelineScope
{
    private readonly PipelineBuilder<TMessage, TScope> builder;
    private readonly List<Envelope> items = new List<Envelope>();
    private readonly object gate = new object();
    private long lastId;

    internal PipelineLayer(PipelineBuilder<TMessage, TScope> builder)
    {
        this.builder = builder;
    }

    public override Type MessageType => typeof(TMessage);

    public IReadOnlyList<Envelope> Items
    {
        get
        {
            lock (gate)
            {
                return items.ToArray();
            }
        }
    }

    public Envelope Add(TMessage message)
    {
        Envelope envelope = new Envelope(NextId(), message, null);
        lock (gate)
        {
            items.Add(envelope);
        }

        return envelope;
    }

    public bool Remove(MessageId id)
    {
        lock (gate)
        {
            return items.RemoveAll(envelope => envelope.Id == id) > 0;
        }
    }

    public bool TryPeek(out Envelope? envelope)
    {
        lock (gate)
        {
            envelope = Next();
            return envelope != null;
        }
    }

    public bool TryTake(out Envelope? envelope)
    {
        lock (gate)
        {
            envelope = Next();
            return envelope != null && items.Remove(envelope);
        }
    }

    public async Task<Envelope?> RunNextAsync(PipelineServices services, CancellationToken ct)
    {
        if (!TryTake(out Envelope? envelope))
        {
            return null;
        }

        return await ExecuteAsync(envelope!, services, ct);
    }

    public Task<Envelope> RunAsync(TMessage message, PipelineServices services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        Envelope? parent = services.Require<PipelineRuns>().Current;
        return ExecuteAsync(new Envelope(NextId(), message, parent), services, ct);
    }

    internal override Task<Envelope> RunMessageAsync(IMessage message, PipelineServices services, CancellationToken ct)
    {
        return RunAsync((TMessage)message, services, ct);
    }

    private static bool Validate(Envelope envelope, PipelineServices services, out string? reason)
    {
        if (services.Resolve(typeof(PipelineValidators)) is PipelineValidators validators)
        {
            return validators.Validate(envelope.Message, out reason);
        }

        reason = null;
        return true;
    }

    private static Type ResultTypeOf(Type command)
    {
        Type[] results = command.GetInterfaces()
            .Where(face => face.IsGenericType && face.GetGenericTypeDefinition() == typeof(ICommand<>))
            .Select(face => face.GetGenericArguments()[0])
            .ToArray();
        if (results.Length != 1)
        {
            throw new InvalidOperationException(
                $"{command.Name} has {results.Length} ICommand<T> interfaces. A layer runs a command with one.");
        }

        return results[0];
    }

    private static Task StepAsync(
        Func<Envelope, TScope, CancellationToken, Task>? step,
        Envelope envelope,
        TScope scope,
        CancellationToken ct)
    {
        return step == null ? Task.CompletedTask : step(envelope, scope, ct);
    }

    private static IPipelineHandler HandlerOf(Envelope envelope, PipelineServices services)
    {
        Type command = envelope.Message.GetType();
        Type result = ResultTypeOf(command);
        string wanted = $"Handler<{command.Name}, {result.Name}, {typeof(TScope).Name}>";
        object handler = services.Resolve(typeof(CommandHandler<,>).MakeGenericType(command, result))
            ?? throw new InvalidOperationException($"{command.Name} has no handler. Add a {wanted}.");
        if (handler is not IPipelineHandler pipelineHandler)
        {
            throw new InvalidOperationException(
                $"{handler.GetType().Name} is a plain CommandHandler. A message of a layer needs a {wanted}.");
        }

        if (pipelineHandler.ScopeType != typeof(TScope))
        {
            throw new InvalidOperationException(
                $"{handler.GetType().Name} takes {pipelineHandler.ScopeType.Name}, "
                + $"but the layer of {typeof(TMessage).Name} gives {typeof(TScope).Name}.");
        }

        return pipelineHandler;
    }

    private MessageId NextId()
    {
        return new MessageId(Interlocked.Increment(ref lastId));
    }

    private Envelope? Next()
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (builder.SelectNext == null)
        {
            return items[0];
        }

        Envelope next = builder.SelectNext(items.ToArray());
        if (next == null || !items.Contains(next))
        {
            throw new InvalidOperationException(
                $"Select of the layer of {typeof(TMessage).Name} must return an envelope of its list.");
        }

        return next;
    }

    private async Task<Envelope> ExecuteAsync(Envelope envelope, PipelineServices services, CancellationToken ct)
    {
        TScope? scope = null;
        envelope.Start();
        services.Require<PipelineRuns>().Current = envelope;
        try
        {
            if (!Validate(envelope, services, out string? reason))
            {
                envelope.Reject(reason);
                return envelope;
            }

            IPipelineHandler handler = HandlerOf(envelope, services);
            scope = builder.CreateScope!(envelope, services)
                ?? throw new InvalidOperationException(
                    $"Scope of the layer of {typeof(TMessage).Name} returned null.");
            scope.Open(envelope);
            await StepAsync(builder.BeforeStep, envelope, scope, ct);
            object? result = await handler.RunAsync(envelope.Message, scope, ct);
            await StepAsync(builder.AfterStep, envelope, scope, ct);
            envelope.Accept(result);
            return envelope;
        }
        catch
        {
            envelope.Fault();
            throw;
        }
        finally
        {
            scope?.Close();
        }
    }
}
