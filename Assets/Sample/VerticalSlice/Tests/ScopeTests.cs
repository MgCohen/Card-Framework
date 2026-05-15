#nullable enable
using NUnit.Framework;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    /// <summary>
    /// Sanity-checks the gatekeeping the scope interfaces are supposed to
    /// enforce. Most enforcement is at compile time (you literally cannot
    /// call <c>scope.Run(...)</c> on an <see cref="ICommandScope"/> because
    /// the method doesn't exist) — these tests cover the runtime aspects
    /// and the interactions across scope types.
    /// </summary>
    public class ScopeTests
    {
        [Test]
        public void EffectScope_ResolvesIEntityForReadOnlyAccess()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetHealth(30);

            IEntity? read = null;
            eng.Stack.Push(new StackFrame(new InspectEffect(target, e => read = e)));

            Assert.That(read, Is.Not.Null);
            Assert.That(read!.Get(Tags.Health), Is.EqualTo(30));
        }

        [Test]
        public void CommandScope_ResolvesMutableEntityFromIEntityPayload()
        {
            // This is the core path: a command's payload carries IEntity, but
            // ICommandScope.Resolve upgrades it to mutable Entity for writes.
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetHealth(30);

            eng.Commands.Execute(new DealDamage(null, target, 6));

            Assert.That(target.Health(), Is.EqualTo(24));
        }

        [Test]
        public void HookScope_CanRunReactiveEffect_NotExecuteCommand()
        {
            // The HookScope exposes Run (effect) but NOT Execute (command).
            // We verify by registering a hook that calls scope.Run — that
            // path works. (The "no Execute" property is enforced at compile
            // time; this test covers the positive case.)
            var eng = new GameEngine();
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create(); caster.SetHealth(30); caster.SetMaxHealth(30);
            var target = eng.World.Create(); target.SetHealth(30); target.SetMaxHealth(30);
            caster.SetBacklash(true);

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

            // Backlash effect ran via scope.Run — caster took 1 retaliation damage.
            Assert.That(caster.Health(), Is.EqualTo(29));
        }

        // --- Helper effect for testing ---------------------------------------

        private sealed class InspectEffect : Effect
        {
            public IEntity Target { get; }
            private readonly System.Action<IEntity> _onRun;

            public InspectEffect(IEntity target, System.Action<IEntity> onRun)
            {
                Target = target;
                _onRun = onRun;
            }

            public override void Run(IEffectScope scope)
            {
                var read = scope.Get(Target.Id);
                if (read != null) _onRun(read);
            }
        }
    }
}
