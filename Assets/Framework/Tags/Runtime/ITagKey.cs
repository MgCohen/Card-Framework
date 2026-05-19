using System;

namespace Framework.Tags
{
    public interface ITagKey { }

    public interface ITagKey<T> : ITagKey { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TagSlotAttribute : Attribute
    {
        public int Slot { get; }
        public TagSlotAttribute(int slot) { Slot = slot; }
    }
}
