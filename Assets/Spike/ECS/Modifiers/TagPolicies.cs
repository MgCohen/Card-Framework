#nullable enable
using System.Collections.Generic;

namespace Spike.Modifiers
{
    public enum RebasePolicy
    {
        // Default: refresh leaves it alone. Gameplay (damage, consumption,
        // direct writes) is the only writer. Health, Shields, DivineShield.
        Transient = 0,

        // Refresh resets to Base then replays every modifier's Refresh
        // effect in Order. Only modifier writes flow through here.
        // Attack, Cost, MaxHealth, Defense.
        Rebaseable = 1,
    }

    // Side-car registry — temporary direction per the spike's "Pending /
    // parked" section. Static for the sandbox; per-engine when production
    // pressure says so.
    public static class TagPolicies
    {
        private static readonly Dictionary<int, RebasePolicy> _byslot = new();

        public static void Set(TagKey key, RebasePolicy policy)
            => _byslot[key.Slot] = policy;

        public static RebasePolicy RebaseOf(int slot)
            => _byslot.TryGetValue(slot, out var p) ? p : RebasePolicy.Transient;

        public static void Clear() => _byslot.Clear();

        // Engine convention defaults — call once at engine construction.
        public static void RegisterDefaults()
        {
            Clear();
            Set(Tags.Attack,    RebasePolicy.Rebaseable);
            Set(Tags.Cost,      RebasePolicy.Rebaseable);
            Set(Tags.MaxHealth, RebasePolicy.Rebaseable);
            Set(Tags.Defense,   RebasePolicy.Rebaseable);
            // Everything else is Transient by default — no entry needed.
        }
    }
}
