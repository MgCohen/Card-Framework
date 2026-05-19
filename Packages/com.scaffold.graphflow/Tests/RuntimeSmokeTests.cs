using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class RuntimeSmokeTests
    {
        [TearDown]
        public void TearDown() => TestGraph.DestroyAll();

        [Test]
        public async Task Mode1_Entry_IntToString_Log()
        {
            var entry = new TestEntryRuntime();
            var conv  = new TestIntToStringRuntime();
            var log   = new TestLogDispatcherRuntime();

            var asset = TestGraph.With(entry, conv, log)
                .Flow(entry, "FlowOut", log, "FlowIn")
                .Data(entry, "Value", conv, "Value")
                .Data(conv,  "Result", log, "Message");

            var sink = new CollectingLogSink();
            var runner = new TestBuilder(sink).Build(asset);
            await runner.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("42", sink.Messages[^1]);
        }

        [Test]
        public async Task Mode2_Entry_Echo_Log_BuildPayload_WriteOutputs()
        {
            var entry = new TestEntryRuntime();
            var echo  = new TestEchoDispatcherRuntime();
            var log   = new TestLogDispatcherRuntime();

            var asset = TestGraph.With(entry, echo, log)
                .Flow(entry, "FlowOut",  echo, "FlowIn")
                .Flow(echo,  "FlowOut",  log,  "FlowIn")
                .Data(entry, "Value",    echo, "Magnitude")
                .Data(echo,  "Summary",  log,  "Message");

            var sink = new CollectingLogSink();
            var runner = new TestBuilder(sink).Build(asset);
            await runner.Run(new TestEntry { Value = 42 });

            Assert.AreEqual("echo:42", sink.Messages[^1]);
        }

        [Test]
        public async Task M2_Entry_Not_Branch_Return_TruePath()
        {
            var entry  = new TestEntryRuntime();
            var not    = new Not();
            var branch = new Branch();
            var ret    = new Return<bool>();
            var cancel = new Cancel();

            var asset = TestGraph.With(entry, not, branch, ret, cancel)
                .Flow(entry,  "FlowOut", branch, "In")
                .Flow(branch, "True",    ret,    "In")
                .Flow(branch, "False",   cancel, "In")
                .Data(not,    "Result",  branch, "Condition");

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run<TestEntry, bool>(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Returned, flow.Outcome, "Return path was taken; Outcome should be Returned.");
            Assert.AreEqual(false, flow.Result, "Return.Value is unwired → reads default(bool)=false.");
        }

        [Test]
        public async Task M2_Branch_False_Cancel_Path()
        {
            var entry  = new TestEntryRuntime();
            var notA   = new Not();
            var notB   = new Not();
            var branch = new Branch();
            var ret    = new Return<bool>();
            var cancel = new Cancel();

            var asset = TestGraph.With(entry, notA, notB, branch, ret, cancel)
                .Flow(entry,  "FlowOut", branch, "In")
                .Flow(branch, "True",    ret,    "In")
                .Flow(branch, "False",   cancel, "In")
                .Data(notA,   "Result",  notB,   "Value")
                .Data(notB,   "Result",  branch, "Condition");

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Cancelled, flow.Outcome, "False branch reaches Cancel.");
        }

        [Test]
        public async Task M3_Return_Stores_Bool_Value()
        {
            var entry = new TestEntryRuntime();
            var not   = new Not();
            var ret   = new Return<bool>();

            var asset = TestGraph.With(entry, not, ret)
                .Flow(entry, "FlowOut", ret, "In")
                .Data(not,   "Result",  ret, "Value");

            var runner = new TestBuilder(new CollectingLogSink()).Build(asset);
            var flow = await runner.Run<TestEntry, bool>(new TestEntry { Value = 0 });

            Assert.AreEqual(Outcome.Returned, flow.Outcome);
            Assert.AreEqual(true, flow.Result);
        }
    }
}
