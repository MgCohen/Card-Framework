#nullable enable
namespace VerticalSlice.Engine.Effects
{
    /// <summary>
    /// What the player or a trigger asked for: "Cast Fireball", "Apply
    /// Healing Potion", "Run the Backlash reaction." Distinct from a
    /// <c>Command</c> (the primitive write).
    ///
    /// Effects compose <i>commands</i>, not other effects. Effect-on-effect
    /// composition happens through the trigger system: a hook on a Command
    /// can <c>Run</c> a reactive effect from its <c>IHookScope</c>.
    ///
    /// Authors do not manage scope open/close — <c>EffectService.Run</c>
    /// wraps <see cref="Run"/> in a Block on the wire automatically.
    /// </summary>
    public abstract class Effect
    {
        public abstract void Run(IEffectScope scope);
    }
}
