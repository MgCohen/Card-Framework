using System;
using System.Collections.Generic;
using Framework.Wire;

namespace Framework.Tags.Tests
{
    // ================================================================
    // TEST STUB — not the real Entity API. Exists only to exercise the
    // tag registry + key shape + wire round-trip end-to-end.
    // Delete when Framework.Identity ships a real entity.
    //
    // The Pattern B overloads (`Set<TTag>(value)`, `Has<TTag>()`, etc.)
    // mirror the shape the eventual Entity API will expose verbatim —
    // when that lands, these methods move to Entity and call sites
    // don't change.
    // ================================================================
    internal sealed class TagBagProbe
    {
        readonly Dictionary<int, object> _slots = new();

        public int EntityId;
        public Action<WireEvent> Emit;   // null = no emission

        // ── Pattern A — value-passing (TagKey<T> handle) ────────────────────────────

        public void Set<T>(TagKey<T> key, T value) where T : unmanaged
        {
            _slots[key.Slot] = value;
            Emit?.Invoke(new TagSetEvent<T>
            {
                EntityId = EntityId,
                Slot     = key.Slot,
                Value    = value,
            });
        }

        public T    Get<T>(TagKey<T> key) where T : unmanaged => (T)_slots[key.Slot];
        public bool Has(TagKey key)                            => _slots.ContainsKey(key.Slot);

        public bool Remove(TagKey key)
        {
            if (!_slots.Remove(key.Slot)) return false;
            Emit?.Invoke(new TagRemovedEvent { EntityId = EntityId, Slot = key.Slot });
            return true;
        }

        // ── Pattern B — type-parameter sugar ─────────────────────────────────────────
        // Per-value-type Set overloads avoid the `Set<TTag, T>(v)` two-param form.
        // Get keeps the two-param shape because the return type can't be inferred
        // from a single type argument.

        public void Set<TTag>(bool   value) where TTag : ITagKey<bool>   => Set(Tag<TTag, bool>.Key,   value);
        public void Set<TTag>(int    value) where TTag : ITagKey<int>    => Set(Tag<TTag, int>.Key,    value);

        public T    Get<TTag, T>()          where TTag : ITagKey<T> where T : unmanaged
            => Get(Tag<TTag, T>.Key);

        public bool Has<TTag>()             where TTag : ITagKey       => Has(Tag<TTag>.Key);
        public bool Remove<TTag>()          where TTag : ITagKey       => Remove(Tag<TTag>.Key);

        // ── Wire-side handlers (unchanged) ───────────────────────────────────────────

        public void Apply<T>(TagSetEvent<T> ev) where T : unmanaged
        {
            _slots[ev.Slot] = ev.Value;
        }

        public void Apply(TagRemovedEvent ev) => _slots.Remove(ev.Slot);
    }
}
