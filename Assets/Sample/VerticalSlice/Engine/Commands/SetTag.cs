#nullable enable
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// Universal write primitive. Use named commands (DealDamage, Heal, …)
    /// for hook-discriminable operations; <c>SetTag</c> is for ad-hoc cases
    /// that don't deserve their own command class.
    /// </summary>
    public sealed class SetTag<T> : Command
    {
        public Entity Target { get; }
        public TagKey<T> Key { get; }
        public T Value { get; }

        public SetTag(Entity target, TagKey<T> key, T value)
        {
            Target = target;
            Key = key;
            Value = value;
        }

        public override BlockType BlockType => BlockType.Power;
        public override void Apply(World world) => Target.Set(Key, Value);
    }
}
