#nullable enable
using System;

namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Declares the runtime slot for a Pattern B tag class
    /// (<see cref="ITagKey{T}"/> implementer). The registry reads this at
    /// <c>RegisterType</c> time, claims the slot (collision = throw), and
    /// synthesizes a <c>TagKey&lt;T&gt;</c> singleton bound to it.
    ///
    /// <para>Slot ranges in the slice are an organisational convention,
    /// not enforced: 1-19 kind markers, 20-39 stats, 40-59 plain ability
    /// flags, 60-79 names, 80-99 entity-id refs, 100+ Pattern B
    /// keywords. Pick a free int in the right range.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TagSlotAttribute : Attribute
    {
        public int Slot { get; }
        public string? DebugName { get; }

        public TagSlotAttribute(int slot, string? debugName = null)
        {
            Slot = slot;
            DebugName = debugName;
        }
    }
}
