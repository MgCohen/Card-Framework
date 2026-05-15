#nullable enable
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Direct-trigger payload for "this card is being cast." Attached to
    /// castable cards via <c>CardEffectStore.Set&lt;OnCast&gt;(card, cast
    /// => new SomeEffect(cast.Caster, cast.Target))</c>; the <see cref="PlayCard"/>
    /// command looks the effect up at play time, pushes it onto the
    /// engine stack with the card as <c>SourceCard</c>, and the engine's
    /// stack-resolution pops it.
    ///
    /// Same shape as <see cref="Deathrattle"/> — both are direct-trigger
    /// payloads carrying the context an attached effect-factory needs to
    /// build a specific Effect instance. Different trigger types, one
    /// machinery (CardEffectStore + FireDirect/ScheduleDirect/PlayCard).
    ///
    /// Plain sealed class (not a C# record) because Unity's .NET surface
    /// doesn't ship <c>System.Runtime.CompilerServices.IsExternalInit</c>
    /// by default; records' init-setters require it.
    /// </summary>
    public sealed class OnCast
    {
        public IEntity Caster { get; }
        public IEntity Target { get; }

        public OnCast(IEntity caster, IEntity target)
        {
            Caster = caster;
            Target = target;
        }
    }
}
