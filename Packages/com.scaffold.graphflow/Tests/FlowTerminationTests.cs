#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;

namespace Scaffold.GraphFlow.Tests
{
    // Locks in the runtime contract that flow.Cancel() / flow.Return() stop
    // execution even when the calling node forgets to return null from its
    // FlowIn handler. Without the IsTerminating check in
    // GraphRunner.RunFromInPort, a node author who calls flow.Cancel() and
    // then returns the next FlowOutPort would silently keep the chain going.
    public sealed class FlowTerminationTests
    {
        [TearDown]
        public void TearDown() => TestGraph.DestroyAll();

        // A deliberately-misbehaved node: sets terminating state but returns
        // a non-null next port. Models the bug-prone shape we want the
        // runtime to defend against.
        [System.Serializable]
        public sealed class ForgetfulCancel : RuntimeNode
        {
            public FlowInPort In = null!;
            public FlowOutPort Out = null!;

            public ForgetfulCancel()
            {
                Out = new FlowOutPort(this, nameof(Out));
                In = FlowInPort.Sync(this, nameof(In), flow =>
                {
                    flow.Cancel();
                    return Out;  // intentionally non-null
                });
                Ports.Add(In.Name, In);
                Ports.Add(Out.Name, Out);
            }
        }

        [System.Serializable]
        public sealed class ForgetfulReturn : RuntimeNode
        {
            public FlowInPort In = null!;
            public FlowOutPort Out = null!;

            public ForgetfulReturn()
            {
                Out = new FlowOutPort(this, nameof(Out));
                In = FlowInPort.Sync(this, nameof(In), flow =>
                {
                    flow.Return();
                    return Out;  // intentionally non-null
                });
                Ports.Add(In.Name, In);
                Ports.Add(Out.Name, Out);
            }
        }

        // Records execution so we can assert downstream nodes did NOT run.
        [System.Serializable]
        public sealed class ExecutionMarker : RuntimeNode
        {
            public FlowInPort In = null!;
            public bool Ran;

            public ExecutionMarker()
            {
                In = FlowInPort.Sync(this, nameof(In), flow =>
                {
                    Ran = true;
                    return null;
                });
                Ports.Add(In.Name, In);
            }
        }

        [Test]
        public async Task Cancel_StopsExecution_EvenWhenNodeReturnsNonNullPort()
        {
            var entry = new TestEntryRuntime();
            var bad   = new ForgetfulCancel();
            var mark  = new ExecutionMarker();

            var asset = TestGraph.With(entry, bad, mark)
                .Flow(entry, "FlowOut", bad, "In")
                .Flow(bad,   "Out",     mark, "In");

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Cancelled, flow.Outcome);
            Assert.IsFalse(mark.Ran, "Downstream node ran despite Cancel; runtime did not honor IsTerminating.");
        }

        [Test]
        public async Task Return_StopsExecution_EvenWhenNodeReturnsNonNullPort()
        {
            var entry = new TestEntryRuntime();
            var bad   = new ForgetfulReturn();
            var mark  = new ExecutionMarker();

            var asset = TestGraph.With(entry, bad, mark)
                .Flow(entry, "FlowOut", bad, "In")
                .Flow(bad,   "Out",     mark, "In");

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Returned, flow.Outcome);
            Assert.IsFalse(mark.Ran, "Downstream node ran despite Return; runtime did not honor IsTerminating.");
        }
    }
}
