#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    /// <summary>
    /// Choose-One request mechanic via the Tactician sample. Casting
    /// opens a Request block with three options (damage, heal, cancel).
    /// The request sits in <c>engine.Requests.Open</c> until Respond
    /// fires; the picked option's server-side resolution then runs
    /// top-level.
    /// </summary>
    public class TacticianTests
    {
        [Test]
        public void Cast_OpensRequest_WithThreeOptions()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create(); target.SetHealth(30); target.SetMaxHealth(30);

            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));

            Assert.That(eng.Requests.Open.Count, Is.EqualTo(1));
            var req = eng.Requests.Open.Single().Value;
            Assert.That(req.Name, Is.EqualTo("Tactician"));
            Assert.That(req.Options.Count, Is.EqualTo(3));
            Assert.That(req.Options[0], Is.TypeOf<PickOptionEvent>());
            Assert.That(req.Options[1], Is.TypeOf<PickOptionEvent>());
            Assert.That(req.Options[2], Is.TypeOf<CancelOptionEvent>());
        }

        [Test]
        public void PickDamageOption_DealsDamage_AndClosesRequest()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create(); target.SetHealth(30); target.SetMaxHealth(30);

            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));
            var reqId = eng.Requests.Open.Single().Key;

            eng.Requests.Respond(reqId, picked: 0);

            Assert.That(target.Health(), Is.EqualTo(24));
            Assert.That(eng.Requests.Open, Is.Empty);
        }

        [Test]
        public void PickHealOption_Heals_AndClosesRequest()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetMaxHealth(30); target.SetHealth(20);

            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));
            var reqId = eng.Requests.Open.Single().Key;

            eng.Requests.Respond(reqId, picked: 1);

            Assert.That(target.Health(), Is.EqualTo(26));   // 20 + 6
            Assert.That(eng.Requests.Open, Is.Empty);
        }

        [Test]
        public void PickCancelOption_NoMutation_AndClosesRequest()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetMaxHealth(30); target.SetHealth(30);

            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));
            var reqId = eng.Requests.Open.Single().Key;

            eng.Requests.Respond(reqId, picked: 2);   // cancel

            Assert.That(target.Health(), Is.EqualTo(30));
            Assert.That(eng.Requests.Open, Is.Empty);
        }

        [Test]
        public void RequestWireShape_BlockWithOptionEvents_FollowedByResponseAndCommand()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create(); target.SetHealth(30); target.SetMaxHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));
            var reqId = eng.Requests.Open.Single().Key;
            eng.Requests.Respond(reqId, picked: 0);

            var added = eng.World.Log.Events.Skip(checkpoint).ToList();

            // Find the Request-kind BlockStart.
            var requestStart = added.OfType<BlockStartEvent>()
                                    .First(b => b.BlockKind == BlockKind.Request);
            Assert.That(requestStart.Name, Is.EqualTo("Tactician"));

            // Verify the option events appear inside the Request block.
            var options = added.OfType<OptionEvent>().ToList();
            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(options[0], Is.TypeOf<PickOptionEvent>());
            Assert.That(options[1], Is.TypeOf<PickOptionEvent>());
            Assert.That(options[2], Is.TypeOf<CancelOptionEvent>());

            // Verify Response event appears.
            var response = added.OfType<ResponseEvent>().Single();
            Assert.That(response.RequestId, Is.EqualTo(reqId));
            Assert.That(response.Picked, Is.EqualTo(0));

            // Verify DealDamage command block appears AFTER the response.
            var responseIdx = added.IndexOf(response);
            var damageBlock = added.Skip(responseIdx)
                                   .OfType<BlockStartEvent>()
                                   .FirstOrDefault(b => b.Name == "DealDamage");
            Assert.That(damageBlock, Is.Not.Null,
                "DealDamage block must appear after the Response event.");
        }

        [Test]
        public void PickOption_PayloadCarriesSourceTargetValue()
        {
            // Wire option events should carry the right entity refs and value —
            // no labels, just data.
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create(); target.SetHealth(30); target.SetMaxHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Tactician(caster, target)));

            var pick0 = eng.World.Log.Events.Skip(checkpoint)
                          .OfType<PickOptionEvent>().First();
            Assert.That(pick0.Index, Is.EqualTo(0));
            Assert.That(pick0.SourceId, Is.EqualTo(caster.Id));
            Assert.That(pick0.TargetId, Is.EqualTo(target.Id));
            Assert.That(pick0.Value, Is.EqualTo(TacticianRequest.Damage));
        }
    }
}
