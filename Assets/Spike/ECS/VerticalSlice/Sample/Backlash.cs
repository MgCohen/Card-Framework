#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Backlash retaliation effect. Fired by <see cref="BacklashHook"/>
    /// when a card with <c>Tags.Backlash</c> deals damage. As an Effect,
    /// it opens its own Block named "Backlash" — so the wire records
    /// "this damage happened because Backlash fired", not just "another
    /// damage spontaneously appeared." That's the causal chain we want
    /// for animations and replay.
    ///
    /// Triggers and effects are the same C# shape; the only difference is
    /// who invokes them (player input → Fireball; bus → Backlash).
    /// </summary>
    public sealed class Backlash : Effect
    {
        public const int RetaliationDamage = 1;

        public IEntity Source { get; }
        public IEntity Target { get; }

        public Backlash(IEntity source, IEntity target)
        {
            Source = source;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new DealDamage(Source, Target, RetaliationDamage));
        }
    }
}
