#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Translates the broadcast event "MoveCard to a graveyard zone" into
    /// the targeted direct trigger "fire this card's Deathrattle effect,
    /// if any." Registered as an Effect&lt;MoveCard&gt; hook on After/Reaction.
    ///
    /// <para>When MoveCard's After phase fires, this hook resolves on the
    /// stack as a wire sibling of MoveCard. If <see cref="Validate"/>
    /// confirms the destination is a graveyard, <see cref="Run"/> fires
    /// the card's stored Deathrattle effect inline — that effect's block
    /// nests inside this bridge's block on the wire.</para>
    /// </summary>
    public sealed class DeathrattleBridge : Effect<MoveCard>
    {
        public DeathrattleBridge(MoveCard cmd) : base(cmd) { }

        public override bool Validate(IEffectScope scope)
            => Trigger.Destination.IsGraveyard()
            && Trigger.Card.Has<Deathrattle>()                  // tag = gate
            && scope.HasDirect<Deathrattle>(Trigger.Card);      // factory = body

        public override void Run(IEffectScope scope)
            => scope.FireDirect(Trigger.Card, new Deathrattle(Trigger.Card));

        public static void Register(EffectStore store)
            => store.Subscribe<MoveCard>(
                new HookConfig
                {
                    Phase = HookPhase.After,
                    Priority = HookPriority.Reaction,
                    Source = nameof(DeathrattleBridge),
                },
                cmd => new DeathrattleBridge(cmd));
    }
}
