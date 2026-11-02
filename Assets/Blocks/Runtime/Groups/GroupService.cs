using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.RecordIds;
using Framework.Wire;

namespace Framework.Blocks
{
    // Server only. BlockPositions must subscribe first on the same stream.
    public sealed class GroupService
    {
        readonly IEventEmitter _events;
        readonly Stack<BlockId> _open = new();

        public GroupService(IEventEmitter events) => _events = events;

        // Null when no group is open.
        public BlockId Current => _open.Count > 0 ? _open.Peek() : null;

        public BlockId Open(IEntity source = null, BlockId trigger = null)
        {
            var start = new GroupStart { TriggerId = trigger, SourceId = source?.Id };
            _events.Emit(start);
            // Setup error: without BlockPositions, the GroupStart is already on the stream when this throws.
            if (start.Position == 0)
                throw new InvalidOperationException("GroupStart has no position. Attach BlockPositions to the stream first.");
            _open.Push(start.Id);
            return start.Id;
        }

        // If id has open child groups, closes them and id as Errored, then throws.
        public void Close(BlockId id, GroupOutcome outcome = GroupOutcome.Completed)
        {
            if (id == null || !_open.Contains(id))
                throw new InvalidOperationException($"Group {id?.Value} is not open.");
            if (Current != id)
            {
                CloseFrom(id, GroupOutcome.Errored);
                throw new InvalidOperationException($"Group {id.Value} had open child groups. All closed as Errored.");
            }
            End(id, outcome);
        }

        public void Create(Action logic, IEntity source = null, BlockId trigger = null)
            => Create<object>(() => { logic(); return null; }, source, trigger);

        public T Create<T>(Func<T> logic, IEntity source = null, BlockId trigger = null)
        {
            var id = Open(source, trigger);
            T result;
            try { result = logic(); }
            catch (OperationCanceledException) { CloseFrom(id, GroupOutcome.Cancelled); throw; }
            catch { CloseFrom(id, GroupOutcome.Errored); throw; }
            Close(id);
            return result;
        }

        public Task CreateAsync(Func<Task> logic, IEntity source = null, BlockId trigger = null)
            => CreateAsync<object>(async () => { await logic(); return null; }, source, trigger);

        public async Task<T> CreateAsync<T>(Func<Task<T>> logic, IEntity source = null, BlockId trigger = null)
        {
            var id = Open(source, trigger);
            T result;
            try { result = await logic(); }
            catch (OperationCanceledException) { CloseFrom(id, GroupOutcome.Cancelled); throw; }
            catch { CloseFrom(id, GroupOutcome.Errored); throw; }
            Close(id);
            return result;
        }

        // Closes the open child groups of id, then id, all with the same outcome.
        void CloseFrom(BlockId id, GroupOutcome outcome)
        {
            if (!_open.Contains(id)) return;
            while (Current != id) End(Current, outcome);
            End(id, outcome);
        }

        void End(BlockId id, GroupOutcome outcome)
        {
            _open.Pop();
            _events.Emit(new GroupEnd { StartId = id, Outcome = (byte)outcome });
        }
    }
}
