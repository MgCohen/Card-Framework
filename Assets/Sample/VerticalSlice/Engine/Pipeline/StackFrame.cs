#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// One entry on the engine stack. Carries the effect to resolve plus
    /// the metadata the engine needs to wire it up:
    ///
    /// <para><see cref="SourceCard"/> — the card whose play / triggered
    /// ability put this effect on the stack. Set for casts (the spell card
    /// itself); left null for triggered abilities tied to a permanent that
    /// stays in place (Deathrattle, Backlash). After the effect resolves,
    /// <see cref="Stack.OnCastResolved"/> fires for non-null
    /// <see cref="SourceCard"/> — the slice's bootstrap uses this hook to
    /// move the spell card to its owner's graveyard.</para>
    ///
    /// <para><see cref="ParentBlockId"/> — id of the wire block that
    /// caused this frame to be pushed (the in-flight command's block for
    /// After-phase reactions). When the engine pops this frame and opens
    /// the effect's block, this id rides on the <c>BlockStart</c> as
    /// <c>parentId</c> — so the wire's causal chain keeps threading
    /// across siblings, even though stack frames resolve at depth 0.</para>
    /// </summary>
    public readonly struct StackFrame
    {
        public readonly Effect Effect;
        public readonly IEntity? SourceCard;
        public readonly long? ParentBlockId;

        public StackFrame(Effect effect, IEntity? sourceCard = null, long? parentBlockId = null)
        {
            Effect = effect;
            SourceCard = sourceCard;
            ParentBlockId = parentBlockId;
        }
    }
}
