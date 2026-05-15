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
                    // After-phase reaction → schedules on the engine stack
                    // so the deathrattle resolves as its own top-level frame
                    // (sibling of MoveCard), not nested inside MoveCard's
                    // wire block. ParentId on the deathrattle's BlockStart
                    // still threads back to MoveCard for causal traceability.
                    scope.ScheduleDirect(cmd.Card, new Deathrattle(cmd.Card));
                });
        }
    }
}
