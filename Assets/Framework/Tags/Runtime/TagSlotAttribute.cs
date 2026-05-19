using System;

namespace Framework.Tags
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TagSlotAttribute : Attribute
    {
        public int Slot { get; }
        public TagSlotAttribute(int slot) { Slot = slot; }
    }
}
