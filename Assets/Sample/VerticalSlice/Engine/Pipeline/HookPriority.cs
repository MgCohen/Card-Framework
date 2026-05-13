#nullable enable
using System;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Priority bands for hook dispatch within a single phase.
    /// Replacement-band hooks run first and may mutate or cancel the command;
    /// Reaction-band hooks run last and typically issue follow-up commands.
    /// Typed identifier (no enum) so games can extend.
    /// </summary>
    public sealed class HookPriority : IComparable<HookPriority>
    {
        public string Name { get; }
        public int Order { get; }

        private HookPriority(string name, int order) { Name = name; Order = order; }

        public static readonly HookPriority Replacement = new("Replacement", 0);
        public static readonly HookPriority PreEffect   = new("PreEffect",  10);
        public static readonly HookPriority OnEffect    = new("OnEffect",   20);
        public static readonly HookPriority Reaction    = new("Reaction",   30);

        public int CompareTo(HookPriority? other) => Order.CompareTo(other?.Order ?? 0);
        public override string ToString() => Name;
    }
}
