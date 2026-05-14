#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample Choose-One effect. Casting Tactician opens a
    /// <see cref="TacticianRequest"/> with three options (damage, heal,
    /// cancel). The cast's body returns immediately after opening the
    /// request — synchronous slice model. The request sits in
    /// <c>engine.Requests.Open</c> until the player picks an option,
    /// at which point the chosen option's resolution runs top-level.
    /// </summary>
    public sealed class Tactician : Effect
    {
        public IEntity? Source { get; }
        public IEntity Target { get; }

        public Tactician(IEntity? source, IEntity target)
        {
            Source = source;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            // A request can only carry IEntity payloads; coerce non-null
            // source for the sample. (Real cards would always have a caster.)
            scope.OpenRequest(new TacticianRequest(Source ?? Target, Target));
        }
    }
}
