#nullable enable
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Pattern B keyword: dual-role class — type handle, query handle,
    /// and event-payload constructor all rolled into one.
    ///
    /// <list type="bullet">
    /// <item><b>Type handle.</b> <c>card.Set&lt;Deathrattle&gt;(true)</c>
    /// / <c>card.Has&lt;Deathrattle&gt;()</c> use this class as the tag
    /// key (routes through <c>Tag&lt;Deathrattle&gt;.Key</c>).</item>
    /// <item><b>Query handle.</b>
    /// <c>store.With(Tag&lt;Deathrattle&gt;.Key)</c> returns every entity
    /// currently flagged as having a deathrattle.</item>
    /// <item><b>Event payload.</b>
    /// <c>scope.FireDirect(card, new Deathrattle(card))</c> hands a
    /// payload instance to the card's stored deathrattle effect; the
    /// effect reads <see cref="Source"/> (the dying entity).</item>
    /// </list>
    ///
    /// <para>Slot 100 — the first Pattern B keyword. The registry
    /// synthesizes a hidden <c>TagKey&lt;bool&gt;</c> at this slot when
    /// <c>Tag&lt;Deathrattle&gt;.Key</c> is first touched.</para>
    ///
    /// <para>Instance fields (<see cref="Source"/>) are transient event
    /// payload — they live on the firing event only, NOT on the entity's
    /// slot. The slot stores the bool flag; "has deathrattle" is queried
    /// via the tag, "what does its deathrattle do" lives in the
    /// <c>EffectStore.SetDirect&lt;Deathrattle&gt;</c> registration.</para>
    /// </summary>
    [TagSlot(100)]
    public sealed class Deathrattle : ITagKey<bool>
    {
        public IEntity Source { get; }
        public Deathrattle(IEntity source) { Source = source; }
    }
}
