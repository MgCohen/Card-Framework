namespace Peers.Handlers.Commands;

// Runs the work of one match one item at a time, in the order of Run calls, from any thread. On an empty queue, the
// item starts inline on the caller thread, after Run releases its lock. An item that throws faults Idle, and no later
// item runs. Issue #248 moves this queue into a pipeline step that runs before validation.
internal sealed class MatchQueue
{
    private readonly object gate = new object();
    private readonly ExecutionContext? clean;
    private Task tail = Task.CompletedTask;

    public MatchQueue()
    {
        clean = ExecutionContext.Capture();
    }

    public Task Idle
    {
        get
        {
            lock (gate)
            {
                return tail;
            }
        }
    }

    public void Run(Func<Task> work)
    {
        TaskCompletionSource start = new TaskCompletionSource();
        lock (gate)
        {
            Task previous = tail;
            if (clean == null)
            {
                tail = AfterAsync(previous, start.Task, work);
            }
            else
            {
                // Each item starts in the context of the constructor, so a client envelope never has the envelope of
                // another peer as its parent.
                ExecutionContext.Run(clean.CreateCopy(), _ => tail = AfterAsync(previous, start.Task, work), null);
            }
        }

        // The item is the tail before it starts, so a Run call from inside the item goes after it.
        start.SetResult();
    }

    private static async Task AfterAsync(Task previous, Task start, Func<Task> work)
    {
        await start;
        await previous;
        await work();
    }
}
