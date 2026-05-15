#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// "Play this card from hand." The act of playing is a state mutation
    /// (move the card out of its current zone) plus an instruction to the
    /// engine ("here's the cast effect, resolve when ready"). Playing
    /// itself never executes the effect — that's the engine's job, when
    /// it pops the stack frame.
    ///
    /// <para>Flow:
    /// <list type="number">
    /// <item>Look up the card's <see cref="OnCast"/> effect via the
    /// <see cref="ICommandScope.CardEffects"/> store. If none is
    /// registered, the card isn't castable — Apply bails. (Authoring
    /// validation belongs upstream of dispatch.)</item>
    /// <item>Move the card to limbo (zone = 0) — it leaves the hand
    /// but isn't yet in the graveyard. Visible state: "currently
    /// resolving."</item>
    /// <item>Push the cast effect onto the engine stack, tagging the
    /// card itself as <see cref="StackFrame.SourceCard"/>.</item>
    /// </list>
    /// </para>
    ///
    /// After the engine pops the frame and runs the effect, the stack's
    /// <see cref="Stack.OnCastResolved"/> hook fires with the source
    /// card — the slice's bootstrap wires that to a MoveCard into the
    /// owner's graveyard, completing hand → limbo → graveyard.
    /// </summary>
    public sealed class PlayCard : Command
    {
        public IEntity Card { get; }
        public IEntity Caster { get; }
        public IEntity Target { get; }

        public PlayCard(IEntity card, IEntity caster, IEntity target)
        {
            Card = card;
            Caster = caster;
            Target = target;
        }

        public override void Apply(ICommandScope scope)
        {
            var card = scope.Resolve(Card);
            if (card == null) return;

            // Build the cast effect from the card's attached OnCast factory.
            // If the card has no OnCast registered, it's not castable — silent
            // no-op for now. Real authoring would validate upstream.
            var effect = scope.CardEffects.Build(card, new OnCast(Caster, Target));
            if (effect == null) return;

            // Card leaves hand → limbo (zone = 0). Stack.OnCastResolved moves
            // it to the graveyard after the effect resolves.
            card.Set(Tags.Zone, 0);

            // Engine handles resolution timing per the game's stack policy.
            scope.Stack.Push(new StackFrame(effect, sourceCard: card));
        }
    }
}
