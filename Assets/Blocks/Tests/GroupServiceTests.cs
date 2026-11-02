using System;
using System.Threading.Tasks;
using Framework.RecordIds;
using Framework.Wire;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class GroupServiceTests
    {
        GroupWire _wire;
        GroupService Groups => _wire.Groups;

        [SetUp]
        public void SetUp() => _wire = new GroupWire();

        [Test]
        public void Open_ReturnsStartPosition_OnBothSides()
        {
            var id = Groups.Open();

            Assert.AreEqual(new BlockId { Value = 1 }, id);
            Assert.AreEqual(id, _wire.ClientBlocks()[0].Id);
        }

        [Test]
        public void Close_WritesStartIdAndCompleted()
        {
            var id = Groups.Open();

            Groups.Close(id);

            var end = (GroupEnd)_wire.ClientBlocks()[1];
            Assert.AreEqual(2, end.Position);
            Assert.AreEqual(id, end.StartId);
            Assert.AreEqual(GroupOutcome.Completed, (GroupOutcome)end.Outcome);
        }

        [Test]
        public void Open_WritesSourceAndTrigger()
        {
            var trigger = Groups.Open();

            Groups.Open(new TestEntity(4), trigger);
            Groups.Open();

            var blocks = _wire.ClientBlocks();
            Assert.AreEqual(new EntityId { Value = 4 }, ((GroupStart)blocks[1]).SourceId);
            Assert.AreEqual(trigger, ((GroupStart)blocks[1]).TriggerId);
            Assert.IsNull(((GroupStart)blocks[2]).SourceId);
            Assert.IsNull(((GroupStart)blocks[2]).TriggerId);
        }

        [Test]
        public void NestedCreate_InnerStartAfterOuterStart()
        {
            Groups.Create(() => Groups.Create(() => { }));

            var blocks = _wire.ClientBlocks();
            Assert.AreEqual(4, blocks.Count);
            Assert.IsInstanceOf<GroupStart>(blocks[0]);
            Assert.IsInstanceOf<GroupStart>(blocks[1]);
            Assert.AreEqual(blocks[1].Id, ((GroupEnd)blocks[2]).StartId);
            Assert.AreEqual(blocks[0].Id, ((GroupEnd)blocks[3]).StartId);
        }

        [Test]
        public void Throw_ClosesAsErrored_AndRethrowsOriginal()
        {
            var error = new TestError();

            var thrown = Assert.Throws<TestError>(() => Groups.Create(() => throw error));

            Assert.AreSame(error, thrown);
            CollectionAssert.AreEqual(new[] { GroupOutcome.Errored }, _wire.ClientOutcomes());
        }

        [Test]
        public void Throw_WithOpenChild_ClosesBothAsErrored()
        {
            Assert.Throws<TestError>(() => Groups.Create(() =>
            {
                Groups.Open();
                throw new TestError();
            }));

            var blocks = _wire.ClientBlocks();
            Assert.AreEqual(blocks[1].Id, ((GroupEnd)blocks[2]).StartId);
            Assert.AreEqual(blocks[0].Id, ((GroupEnd)blocks[3]).StartId);
            CollectionAssert.AreEqual(new[] { GroupOutcome.Errored, GroupOutcome.Errored }, _wire.ClientOutcomes());
        }

        [Test]
        public void Cancel_ClosesAsCancelled()
        {
            Assert.Throws<OperationCanceledException>(() => Groups.Create(() => throw new OperationCanceledException()));

            CollectionAssert.AreEqual(new[] { GroupOutcome.Cancelled }, _wire.ClientOutcomes());
        }

        [Test]
        public void Close_WithOpenChild_Throws()
        {
            var outer = Groups.Open();
            Groups.Open();

            Assert.Throws<InvalidOperationException>(() => Groups.Close(outer));
        }

        [Test]
        public void Close_WithOpenChild_ClosesChildThenGroupAsErrored()
        {
            var outer = Groups.Open();
            var inner = Groups.Open();

            Assert.Throws<InvalidOperationException>(() => Groups.Close(outer));

            var blocks = _wire.ClientBlocks();
            Assert.AreEqual(inner, ((GroupEnd)blocks[2]).StartId);
            Assert.AreEqual(outer, ((GroupEnd)blocks[3]).StartId);
            CollectionAssert.AreEqual(new[] { GroupOutcome.Errored, GroupOutcome.Errored }, _wire.ClientOutcomes());
            Assert.IsNull(Groups.Current);
        }

        [Test]
        public void Create_LeavesManualChildOpen_ClosesBothAsErrored()
        {
            Assert.Throws<InvalidOperationException>(() => Groups.Create(() => Groups.Open()));

            CollectionAssert.AreEqual(new[] { GroupOutcome.Errored, GroupOutcome.Errored }, _wire.ClientOutcomes());
            Assert.IsNull(Groups.Current);
        }

        [Test]
        public void Close_WithNoOpenGroup_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Groups.Close(new BlockId { Value = 1 }));

            Assert.AreEqual(0, _wire.Client.Count);
        }

        [Test]
        public void Close_SameGroupTwice_Throws()
        {
            var id = Groups.Open();
            Groups.Close(id);

            Assert.Throws<InvalidOperationException>(() => Groups.Close(id));

            Assert.AreEqual(2, _wire.Client.Count);
        }

        [Test]
        public void Open_WithoutBlockPositions_Throws()
        {
            var groups = new GroupService(new EventStream());

            Assert.Throws<InvalidOperationException>(() => groups.Open());
        }

        [Test]
        public async Task CreateAsync_ClosesAfterAwait()
        {
            var gate = new TaskCompletionSource<bool>();

            var run = Groups.CreateAsync(async () => await gate.Task);
            Assert.AreEqual(1, _wire.Client.Count);
            gate.SetResult(true);
            await run;

            CollectionAssert.AreEqual(new[] { GroupOutcome.Completed }, _wire.ClientOutcomes());
        }

        [Test]
        public void Current_FollowsTheInnermostOpenGroup()
        {
            Assert.IsNull(Groups.Current);

            var outer = Groups.Open();
            Groups.Create(() => Assert.AreEqual(new BlockId { Value = 2 }, Groups.Current));
            Assert.AreEqual(outer, Groups.Current);

            Groups.Close(outer);
            Assert.IsNull(Groups.Current);
        }

        [Test]
        public void CreateOfT_ReturnsTheValue()
        {
            Assert.AreEqual(7, Groups.Create(() => 7));

            CollectionAssert.AreEqual(new[] { GroupOutcome.Completed }, _wire.ClientOutcomes());
        }

        [Test]
        public async Task CreateAsyncOfT_ReturnsTheValue()
        {
            Assert.AreEqual(7, await Groups.CreateAsync(() => Task.FromResult(7)));

            CollectionAssert.AreEqual(new[] { GroupOutcome.Completed }, _wire.ClientOutcomes());
        }

        [Test]
        public void CreateAsync_Throw_ClosesAsErrored()
        {
            Assert.ThrowsAsync<TestError>(() => Groups.CreateAsync(() => Task.FromException(new TestError())));

            CollectionAssert.AreEqual(new[] { GroupOutcome.Errored }, _wire.ClientOutcomes());
        }
    }
}
