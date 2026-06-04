using System;
using System.Collections.Generic;
using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;

namespace Framework.Commands.Tests
{
    // Proves the command intake path: Execute opens a Command block, runs the handler (which
    // calls a real service), the mutation lands on the stream, and Execute closes the block —
    // in that order. The mutation itself already mirrors via the Tags tests.
    public class CommandServiceTests
    {
        // Handlers are built from the live TagService so their mutation lands on the same stream
        // the BlockScope emits on — bracket and mutation interleave in true emission order.
        static CommandService Build(out List<WireEvent> log,
            Func<TagService, ICommandHandler[]> handlers,
            Func<TagService, ICommandValidator[]> validators = null)
        {
            var stream   = new EventStream();
            var captured = new List<WireEvent>();
            stream.OnAppended += captured.Add;
            log = captured;

            // Validators take the same live TagService as handlers — a stateful validator must
            // read the very store the handlers mutate, so both are built from one instance.
            var tags       = new TagService(new TagStore(), stream);
            var registry   = new CommandHandlerRegistry(handlers(tags));
            var validation = new CommandValidatorRegistry(
                validators?.Invoke(tags) ?? Array.Empty<ICommandValidator>());
            var blocks     = new BlockScope(stream);
            return new CommandService(registry, validation, blocks);
        }

        static CommandService Build(out List<WireEvent> log)
            => Build(out log, tags => new ICommandHandler[]
            {
                new SetHealthHandler(tags),
                new BoomHandler(),
            });

        [Test]
        public void Execute_Brackets_The_Mutation_In_Stream_Order()
        {
            var svc = Build(out var log);

            svc.Execute(new SetHealth { EntityId = 7, Value = 30 });

            Assert.AreEqual(3, log.Count, "BlockStart, mutation, BlockEnd");

            Assert.IsInstanceOf<BlockStartEvent>(log[0]);
            var start = (BlockStartEvent)log[0];
            Assert.AreEqual((byte)BlockKind.Command, start.Kind);

            Assert.IsInstanceOf<TagSetEvent<int>>(log[1]);
            var set = (TagSetEvent<int>)log[1];
            Assert.AreEqual(7,  set.EntityId);
            Assert.AreEqual(30, set.Value);

            Assert.IsInstanceOf<BlockEndEvent>(log[2]);
            var end = (BlockEndEvent)log[2];
            Assert.AreEqual((byte)BlockOutcome.Completed, end.Outcome);

            Assert.AreEqual(start.Id, end.Id, "open and close share the block id");
        }

        [Test]
        public void Execute_Returns_Accepted()
        {
            var svc = Build(out _);

            var result = svc.Execute(new SetHealth { EntityId = 1, Value = 1 });

            Assert.IsTrue(result.Accepted);
            Assert.IsNull(result.RejectReason);
        }

        [Test]
        public void Handler_Throwing_Closes_The_Block_Errored_And_Propagates()
        {
            var svc = Build(out var log);

            Assert.Throws<BoomException>(() => svc.Execute(new Boom()));

            Assert.AreEqual(2, log.Count, "BlockStart then BlockEnd; no mutation in between");
            Assert.IsInstanceOf<BlockStartEvent>(log[0]);
            Assert.IsInstanceOf<BlockEndEvent>(log[1]);
            var end = (BlockEndEvent)log[1];
            Assert.AreEqual((byte)BlockOutcome.Errored, end.Outcome);
        }

        [Test]
        public void Block_Ids_Are_Monotonic_Across_Executes()
        {
            var svc = Build(out var log);

            svc.Execute(new SetHealth { EntityId = 1, Value = 1 });
            svc.Execute(new SetHealth { EntityId = 2, Value = 2 });

            var ids = new List<int>();
            foreach (var e in log)
                if (e is BlockStartEvent bs) ids.Add(bs.Id);

            Assert.AreEqual(new[] { 1, 2 }, ids, "BlockScope allocates monotonic ids across commands");
        }

        [Test]
        public void Unregistered_Command_Throws()
        {
            var svc = Build(out _, tags => Array.Empty<ICommandHandler>());

            Assert.Throws<InvalidOperationException>(() => svc.Execute(new SetHealth()));
        }

        [Test]
        public void Rejected_Command_Emits_Nothing()
        {
            var svc = Build(out var log,
                tags => new ICommandHandler[] { new SetHealthHandler(tags) },
                _ => new ICommandValidator[] { new SetHealthValidator() });

            var result = svc.Execute(new SetHealth { EntityId = 1, Value = -5 });

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("NegativeHealth", result.RejectReason);
            Assert.IsEmpty(log, "a rejected command opens no block and runs no handler");
        }

        [Test]
        public void Accepted_Command_With_Validator_Brackets_Normally()
        {
            var svc = Build(out var log,
                tags => new ICommandHandler[] { new SetHealthHandler(tags) },
                _ => new ICommandValidator[] { new SetHealthValidator() });

            var result = svc.Execute(new SetHealth { EntityId = 1, Value = 10 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, log.Count, "BlockStart, mutation, BlockEnd — validator accepted");
        }

        [Test]
        public void Stateful_Validator_Rejects_When_Target_Has_No_Health()
        {
            // The validator injects TagService and rejects an entity that carries no Health tag —
            // a decision made on world state, not the command payload. Same zero-traffic invariant
            // as a payload reject: no block opens, no handler runs.
            var svc = Build(out var log,
                tags => new ICommandHandler[] { new DealDamageHandler(tags) },
                tags => new ICommandValidator[] { new DealDamageValidator(tags) });

            var result = svc.Execute(new DealDamage { EntityId = 1, Amount = 5 });

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("NotDamageable", result.RejectReason);
            Assert.IsEmpty(log, "a state-rejected command opens no block and runs no handler");
        }

        // The accept path for a stateful validator — and the handler's read-modify-write — needs an
        // applied store (Set emits; the apply-handler writes the store). This harness wires no
        // apply-loop, so that proof lives in the integration spines (EngineStack / BootstrapWiring),
        // where DealDamage takes the player 30 -> 18 and the value is read back. Kept there on
        // purpose rather than seeding the store here, which the unit suite never does.
    }
}
