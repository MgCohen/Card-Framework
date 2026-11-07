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
    private long lastId;

    internal PipelineLayer(PipelineBuilder<TMessage, TScope> builder)
    {
        this.builder = builder;
    }

    public override Type MessageType => typeof(TMessage);

    public IReadOnlyList<Envelope> Items => items;

    public Envelope Add(TMessage message)
    {
        Envelope e = new Envelope(NextId(), message, null);
        items.Add(e);
        return e;
    }

    public bool Remove(MessageId id)
    {
        return items.RemoveAll(e => e.Id == id) > 0;
    }

    public bool TryPeek(out Envelope? envelope)
    {
        envelope = Next();
        return envelope != null;
    }

    public bool TryTake(out Envelope? envelope)
    {
        envelope = Next();
        return envelope != null && items.Remove(envelope);
    }

    public async Task<Envelope?> RunNextAsync(PipelineServices services, CancellationToken ct)
    {
        if (!TryTake(out Envelope? e))
        {
            return null;
        }

        return await ExecuteAsync(e!, services, ct);
    }

    public Task<Envelope> RunAsync(TMessage message, PipelineServices services, CancellationToken ct)
    {
        return ExecuteAsync(new Envelope(NextId(), message, CurrentRun), services, ct);
    }

    internal override Task<Envelope> RunMessageAsync(IMessage message, PipelineServices services, CancellationToken ct)
    {
        return RunAsync((TMessage)message, services, ct);
    }

    private static bool Validate(Envelope e, PipelineServices services, out string? reason)
    {
        if (services.Resolve(typeof(PipelineValidators)) is PipelineValidators validators)
        {
            return validators.Validate(e.Message, out reason);
        }

        reason = null;
        return true;
    }

    private static Type ResultTypeOf(Type command)
    {
        foreach (Type i in command.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
            {
                return i.GetGenericArguments()[0];
            }
        }

        throw new InvalidOperationException($"{command.Name} is not an ICommand<T>. A layer runs only commands.");
    }

    private static Task StepAsync(
        Func<Envelope, TScope, CancellationToken, Task>? step,
        Envelope e,
        TScope scope,
        CancellationToken ct)
    {
        return step == null ? Task.CompletedTask : step(e, scope, ct);
    }

    private static IPipelineHandler HandlerOf(Envelope e, PipelineServices services)
    {
        Type command = e.Message.GetType();
        Type result = ResultTypeOf(command);
        string wanted = $"Handler<{command.Name}, {result.Name}, {typeof(TScope).Name}>";
        object handler = services.Resolve(typeof(CommandHandler<,>).MakeGenericType(command, result))
            ?? throw new InvalidOperationException($"{command.Name} has no handler. Add a {wanted}.");
        if (handler is not IPipelineHandler pipelineHandler)
        {
            throw new InvalidOperationException(
                $"{handler.GetType().Name} is a plain CommandHandler. A message of a layer needs a {wanted}.");
        }

        if (!pipelineHandler.ScopeType.IsAssignableFrom(typeof(TScope)))
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

        return builder.SelectNext == null ? items[0] : builder.SelectNext(items);
    }

    private async Task<Envelope> ExecuteAsync(Envelope e, PipelineServices services, CancellationToken ct)
    {
        TScope? scope = null;
        e.Start();
        CurrentRun = e;
        try
        {
            if (!Validate(e, services, out string? reason))
            {
                e.Reject(reason);
                return e;
            }

            scope = builder.CreateScope!(e, services)
                ?? throw new InvalidOperationException($"Scope of the layer of {typeof(TMessage).Name} returned null.");
            scope.Open(e);
            await StepAsync(builder.BeforeStep, e, scope, ct);
            object? result = await HandlerOf(e, services).RunAsync(e.Message, scope, ct);
            await StepAsync(builder.AfterStep, e, scope, ct);
            e.Accept(result);
            return e;
        }
        catch
        {
            e.Fault();
            throw;
        }
        finally
        {
            scope?.Close();
        }
    }
}
