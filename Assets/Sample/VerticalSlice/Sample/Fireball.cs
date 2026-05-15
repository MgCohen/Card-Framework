#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// The slice's sample damage effect. Reaches the engine through a
    /// stack frame — either pushed by <see cref="PlayCard"/> when a
    /// Fireball card is played from hand (OnCast factory), or pushed
    /// directly via <c>engine.Stack.Push(new StackFrame(new Fireball(...)))</c>
    /// in tests. When the engine pops the frame, <c>EffectService</c>
    /// opens a Block named "Fireball" with {Source, Target} payload and
    /// runs the body — which issues a single <see cref="DealDamage"/>
    /// command inline. That command opens its own nested block; hooks
    /// (Shield, Armor, Backlash) wire in there.
    /// </summary>
    public sealed class Fireball : Effect
    {
        public const int Damage = 6;

        public IEntity? Source { get; }
        public IEntity Target { get; }

        public Fireball(IEntity? source, IEntity target)
        {
            Source = source;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new DealDamage(Source, Target, Damage));
        }
    }
}
