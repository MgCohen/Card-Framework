#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Engine bridge that translates the broadcast event "MoveCard to a
    /// graveyard zone" into the targeted direct trigger "fire this card's
    /// Deathrattle effect, if any." Hand-wired once at engine setup.
    ///
    /// The bridge is a plain bus subscription, not an <c>Effect</c>, so
    /// it doesn't add its own Block to the wire — only the per-card
    /// effect (DamageAllEnemies / HealAllAllies / etc.) shows up under
    /// the MoveCard block.
    /// </summary>
    public static class DeathrattleBridge
    {
        public static void Register(CommandService commands)
        {
            commands.Subscribe<MoveCard>(HookPhase.After, HookPriority.Reaction,
                (cmd, scope) =>
                {
                    if (!cmd.Destination.IsGraveyard()) return;
                    scope.FireDirect(cmd.Card, new Deathrattle(cmd.Card));
                });
        }
    }
}
