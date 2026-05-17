#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// State-Based Action: any minion at Health ≤ 0 sitting on a
    /// battlefield zone moves to its owner's graveyard. The death cascade
    /// (kill → MoveCard → DeathrattleBridge → DR effect deals more damage
    /// → new deaths next pass) converges naturally through the engine's
    /// drain loop — no per-card "death" wiring needed.
    ///
    /// Once a minion has moved, its zone is no longer a battlefield, so
    /// the next SBA pass skips it. Self-stable.
    ///
    /// Register at engine setup via <see cref="Register"/>; matches the
    /// per-rule registration style of <c>DivineShieldRule</c> /
    /// <c>BacklashRule</c>.
    /// </summary>
    public sealed class DeathSBA : ISBA
    {
        public IEnumerable<Command> Evaluate(IEffectScope scope)
        {
            // Collect first, then resolve graveyards — keeps the yield
            // pattern simple and avoids mutating-while-enumerating quirks
            // even though SBA is read-only against the scope.
            var dying = new List<IEntity>();
            foreach (var e in scope.With(Tags.IsMinion))
            {
                if (e.Health() > 0) continue;
                var zone = scope.Get(e.ZoneId());
                if (zone == null || !zone.IsBattlefield()) continue;
                dying.Add(e);
            }

            foreach (var minion in dying)
            {
                var graveyard = FindGraveyard(scope, minion.OwnerId());
                if (graveyard == null) continue;   // owner has no graveyard — skip
                yield return new MoveCard(minion, graveyard);
            }
        }

        private static IEntity? FindGraveyard(IEffectScope scope, int ownerId)
        {
            // Owner-indexed query → the owner's graveyard, if any. With
            // Tags.Owner registered as a value index, this hits the
            // owner-keyed bucket directly instead of scanning all zones.
            foreach (var z in scope.With(Tags.IsGraveyard).And(Tags.Owner, ownerId))
                return z;
            return null;
        }

        public static void Register(GameEngine engine) => engine.RegisterSBA(new DeathSBA());
    }
}
