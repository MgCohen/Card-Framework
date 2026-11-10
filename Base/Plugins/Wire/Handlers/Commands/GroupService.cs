using Ecs.Domain.Shared;
using Wire.Contracts.Commands;

namespace Wire.Handlers.Commands;

public sealed class GroupService
{
    private readonly WireLog log;
    private readonly Stack<BlockId> open = new Stack<BlockId>();

    public GroupService(WireLog log)
    {
        this.log = log;
    }

    public BlockId Current => open.Count > 0 ? open.Peek() : default;

    public BlockId Open(EntityId source = default, BlockId trigger = default)
    {
        log.Append(new GroupStart(source, trigger));
        BlockId id = new BlockId(log.Entries.Count);
        open.Push(id);
        return id;
    }

    public void Close(BlockId id, GroupOutcome outcome = GroupOutcome.Completed)
    {
        if (id == default || !open.Contains(id))
        {
            throw new InvalidOperationException($"Group {id.Value} is not open.");
        }

        if (Current != id)
        {
            CloseFrom(id, GroupOutcome.Errored);
            throw new InvalidOperationException($"Group {id.Value} had open child groups. All closed as Errored.");
        }

        End(outcome);
    }

    public void Create(Action logic, EntityId source = default, BlockId trigger = default)
    {
        Create<object?>(
            () =>
            {
                logic();
                return null;
            },
            source,
            trigger);
    }

    public T Create<T>(Func<T> logic, EntityId source = default, BlockId trigger = default)
    {
        BlockId id = Open(source, trigger);
        T result;
        try
        {
            result = logic();
        }
        catch (OperationCanceledException)
        {
            CloseFrom(id, GroupOutcome.Cancelled);
            throw;
        }
        catch
        {
            CloseFrom(id, GroupOutcome.Errored);
            throw;
        }

        Close(id);
        return result;
    }

    public Task CreateAsync(Func<Task> logic, EntityId source = default, BlockId trigger = default)
    {
        return CreateAsync<object?>(
            async () =>
            {
                await logic();
                return null;
            },
            source,
            trigger);
    }

    public async Task<T> CreateAsync<T>(Func<Task<T>> logic, EntityId source = default, BlockId trigger = default)
    {
        BlockId id = Open(source, trigger);
        T result;
        try
        {
            result = await logic();
        }
        catch (OperationCanceledException)
        {
            CloseFrom(id, GroupOutcome.Cancelled);
            throw;
        }
        catch
        {
            CloseFrom(id, GroupOutcome.Errored);
            throw;
        }

        Close(id);
        return result;
    }

    private void CloseFrom(BlockId id, GroupOutcome outcome)
    {
        if (!open.Contains(id))
        {
            return;
        }

        while (Current != id)
        {
            End(outcome);
        }

        End(outcome);
    }

    private void End(GroupOutcome outcome)
    {
        BlockId id = open.Pop();
        log.Append(new GroupEnd(id, outcome));
    }
}
