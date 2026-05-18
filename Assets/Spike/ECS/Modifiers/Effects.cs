#nullable enable
using System;
using System.Collections.Generic;

namespace Spike.Modifiers
{
    // Stub for the sandbox. Real shape parked along with the broader
    // effect-data-asset question (see spike doc Q5/Q6 in definition-entity-setup).
    public interface IEffectScope { }

    internal sealed class EffectScope : IEffectScope { }

    // Free-form: an effect can do anything an effect can do — set a tag,
    // add to a tag, multiply, branch, compose. Same shape that cards' OnCast
    // / Deathrattle effects would use.
    public interface IModifierEffect
    {
        void Apply(Entity target, IEffectScope scope);
    }

    // ─── Concrete effects exercised by the four sandbox scenarios ────────

    public sealed class SetBoolEffect : IModifierEffect
    {
        private readonly TagKey<bool> _key;
        private readonly bool         _value;
        public SetBoolEffect(TagKey<bool> key, bool value) { _key = key; _value = value; }
        public void Apply(Entity target, IEffectScope scope) => target.Set(_key, _value);
    }

    public sealed class SetIntEffect : IModifierEffect
    {
        private readonly TagKey<int> _key;
        private readonly int         _value;
        public SetIntEffect(TagKey<int> key, int value) { _key = key; _value = value; }
        public void Apply(Entity target, IEffectScope scope) => target.Set(_key, _value);
    }

    public sealed class AddIntEffect : IModifierEffect
    {
        private readonly TagKey<int> _key;
        private readonly int         _delta;
        public AddIntEffect(TagKey<int> key, int delta) { _key = key; _delta = delta; }
        public void Apply(Entity target, IEffectScope scope)
            => target.Set(_key, target.Get(_key) + _delta);
    }

    public sealed class MulIntEffect : IModifierEffect
    {
        private readonly TagKey<int> _key;
        private readonly int         _factor;
        public MulIntEffect(TagKey<int> key, int factor) { _key = key; _factor = factor; }
        public void Apply(Entity target, IEffectScope scope)
            => target.Set(_key, target.Get(_key) * _factor);
    }

    public sealed class ConditionalEffect : IModifierEffect
    {
        private readonly Func<Entity, bool> _cond;
        private readonly IModifierEffect?   _then;
        private readonly IModifierEffect?   _else;
        public ConditionalEffect(Func<Entity, bool> cond,
                                 IModifierEffect? thenE = null,
                                 IModifierEffect? elseE = null)
        { _cond = cond; _then = thenE; _else = elseE; }

        public void Apply(Entity target, IEffectScope scope)
        {
            if (_cond(target)) _then?.Apply(target, scope);
            else               _else?.Apply(target, scope);
        }
    }

    public sealed class CompositeEffect : IModifierEffect
    {
        private readonly IReadOnlyList<IModifierEffect> _steps;
        public CompositeEffect(params IModifierEffect[] steps) { _steps = steps; }
        public void Apply(Entity target, IEffectScope scope)
        {
            for (int i = 0; i < _steps.Count; i++) _steps[i].Apply(target, scope);
        }
    }
}
