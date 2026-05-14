#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Wires <see cref="Deathrattle"/> to fire on After+Reaction of
    /// <see cref="MoveCard"/>: when a card with <c>HasDeathrattle</c>
    /// moves into a zone tagged <c>IsGraveyard</c>, the trigger runs.
    ///
    /// Per-card gating (<c>cmd.Card.HasDeathrattle()</c>) means moves of
    /// cards without the tag don't pay any cost. Per-destination gating
    /// (<c>cmd.Destination.IsGraveyard()</c>) means moves to non-graveyard
    /// zones don't fire — same card can hop Hand → Battlefield → Hand
    /// without any deathrattles.
    /// </summary>
    public static class DeathrattleRule
    {
        public static void Register(CommandService commands)
        {
            commands.Subscribe<MoveCard>(HookPhase.After, HookPriority.Reaction,
                (cmd, scope) =>
                {
                    if (!cmd.Card.HasDeathrattle()) return;
                    if (!cmd.Destination.IsGraveyard()) return;

                    scope.Run(new Deathrattle(cmd.Card));
                });
        }
    }
}
