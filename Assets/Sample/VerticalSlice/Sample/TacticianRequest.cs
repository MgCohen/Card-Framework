#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// "Choose One" request for the Tactician sample. Three options:
    /// damage, heal, cancel. The wire records each option as a typed
    /// <see cref="OptionEvent"/> (PickOption / CancelOption) with no
    /// label — labels live on the client side. The server-side mapping
    /// from picked-index → command lives in <see cref="Resolve"/>.
    /// </summary>
    public sealed class TacticianRequest : Request
    {
        private readonly IEntity _source;
        private readonly IEntity _target;
        public const int Damage = 6;
        public const int HealAmount = 6;

        public override string Name => "Tactician";
        public override IReadOnlyList<OptionEvent> Options { get; }

        public TacticianRequest(IEntity source, IEntity target)
        {
            _source = source;
            _target = target;
            Options = new OptionEvent[]
            {
                // Option 0 — deal damage
                new PickOptionEvent(0, source: source.Id, target: target.Id, value: Damage),
                // Option 1 — heal
                new PickOptionEvent(1, source: source.Id, target: target.Id, value: HealAmount),
                // Option 2 — cancel (typed-distinct affordance on the client)
                new CancelOptionEvent(2),
            };
        }

        public override void Resolve(int picked, IEffectScope scope)
        {
            if (picked < 0 || picked >= Options.Count) return;
            if (Options[picked] is CancelOptionEvent) return;

            switch (picked)
            {
                case 0: scope.Execute(new DealDamage(_source, _target, Damage)); break;
                case 1: scope.Execute(new Heal(_source, _target, HealAmount)); break;
            }
        }
    }
}
