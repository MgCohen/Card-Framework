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

        public BlockId Open(IEntity source = null, BlockId trigger = null)
        {
            var start = new GroupStart { TriggerId = trigger, SourceId = source?.Id };
            _events.Emit(start);
            if (start.Position == 0)
                throw new InvalidOperationException("GroupStart has no position. Attach BlockPositions to the stream first.");
            _open.Push(start.Id);
            return start.Id;
        }

        public void Close(BlockId id, GroupOutcome outcome = GroupOutcome.Completed)
        {
            if (_open.Count == 0 || _open.Peek() != id)
                throw new InvalidOperationException($"Group {id?.Value} is not the innermost open group.");
            _open.Pop();
            _events.Emit(new GroupEnd { StartId = id, Outcome = (byte)outcome });
        }

        public void Create(IEntity source, Action logic, BlockId trigger = null)
        {
            var id = Open(source, trigger);
            try { logic(); }
            catch (OperationCanceledException) { CloseFrom(id, GroupOutcome.Cancelled); throw; }
            catch { CloseFrom(id, GroupOutcome.Errored); throw; }
            Close(id);
        }

        public async Task CreateAsync(IEntity source, Func<Task> logic, BlockId trigger = null)
        {
            var id = Open(source, trigger);
            try { await logic(); }
            catch (OperationCanceledException) { CloseFrom(id, GroupOutcome.Cancelled); throw; }
            catch { CloseFrom(id, GroupOutcome.Errored); throw; }
            Close(id);
        }

        // Closes the open child groups of id, then id, all with the same outcome.
        void CloseFrom(BlockId id, GroupOutcome outcome)
        {
            if (!_open.Contains(id)) return;
            while (_open.Peek() != id) Close(_open.Peek(), outcome);
            Close(id, outcome);
        }
    }
}
