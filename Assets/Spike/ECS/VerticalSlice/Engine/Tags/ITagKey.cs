#nullable enable
namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Marker interface for Pattern B tags — keyword classes that double
    /// as type handle, query handle, and event payload constructor.
    /// <typeparamref name="T"/> is the value type stored at the tag's
    /// slot (<c>bool</c> for "is this card a Deathrattle / has Poison /
    /// etc"; <c>int</c> for stateful keywords like Poison stacks; …).
    ///
    /// <para>Pattern B classes carry a <c>[TagSlot]</c> attribute at the
    /// class level; the registry reads the attribute to claim the slot
    /// and synthesizes a hidden <c>TagKey&lt;T&gt;</c> singleton at it.
    /// Access via <c>Tag&lt;TTag&gt;.Key</c>.</para>
    /// </summary>
    public interface ITagKey<T> { }
}
