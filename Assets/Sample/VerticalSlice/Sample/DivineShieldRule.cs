#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Divine Shield keyword behaviour. A single Replacement-band hook on
    /// <see cref="DealDamage"/> that zeroes the incoming damage and flips
    /// the shield tag off. The Hp write is then skipped by the
    /// equality-skip guard in <c>Entity.Set</c>.
    /// </summary>
    public static class DivineShieldRule
    {
        public static void Register(SignalBus bus)
        {
            bus.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement, cmd =>
            {
                if (cmd.Amount <= 0) return;
                if (cmd.Target is not Card c) return;
                if (!c.DivineShield) return;

                cmd.Amount = 0;
                c.DivineShield = false;
            });
        }
    }
}
