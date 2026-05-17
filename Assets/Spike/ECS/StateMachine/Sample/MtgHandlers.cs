#nullable enable
using StateMachine.Engine;

namespace StateMachine.Sample
{
    public sealed class PassHandler : ICommandHandler<PassCommand, StandardResult>
    {
        public StandardResult Handle(PassCommand cmd, ICommandScope scope) => StandardResult.Instance;
    }

    public sealed class CommitAttackersHandler : ICommandHandler<CommitAttackersCommand, StandardResult>
    {
        public StandardResult Handle(CommitAttackersCommand cmd, ICommandScope scope) => StandardResult.Instance;
    }

    public sealed class CommitBlockersHandler : ICommandHandler<CommitBlockersCommand, StandardResult>
    {
        public StandardResult Handle(CommitBlockersCommand cmd, ICommandScope scope) => StandardResult.Instance;
    }

    public sealed class ToggleAttackerHandler : ICommandHandler<ToggleAttackerCommand, StandardResult>
    {
        public StandardResult Handle(ToggleAttackerCommand cmd, ICommandScope scope)
        {
            foreach (var e in scope.World.Entities)
            {
                if (e.Id != cmd.CardId) continue;
                // The With() query checks tag-presence, so off-state means
                // the slot is absent, not "present with value=false".
                if (e.Has(MtgTags.IsAttacking)) e.Remove(MtgTags.IsAttacking);
                else                            e.Set   (MtgTags.IsAttacking, true);
                break;
            }
            return StandardResult.Instance;
        }
    }

    public sealed class DealDamageHandler : ICommandHandler<DealDamageCommand, StandardResult>
    {
        public StandardResult Handle(DealDamageCommand cmd, ICommandScope scope)
        {
            foreach (var e in scope.World.Entities)
            {
                if (e.Id != cmd.SourceId) continue;
                e.Set(MtgTags.DamageResolved, true);
                break;
            }
            return StandardResult.Instance;
        }
    }

    public sealed class ClearTagHandler : ICommandHandler<ClearTagCommand, StandardResult>
    {
        public StandardResult Handle(ClearTagCommand cmd, ICommandScope scope)
        {
            // Look up the key dynamically by name. The minimal sample doesn't
            // need a generic version — this is just enough to demonstrate that
            // EndState.OnEnter outputs reach entities.
            foreach (var e in scope.World.Entities)
            {
                switch (cmd.TagName)
                {
                    case "IsAttacking":    e.Remove(MtgTags.IsAttacking);    break;
                    case "DamageResolved": e.Remove(MtgTags.DamageResolved); break;
                    case "Blocking":       e.Remove(MtgTags.Blocking);       break;
                }
            }
            return StandardResult.Instance;
        }
    }

    public sealed class IncrementTurnHandler : ICommandHandler<IncrementTurnCommand, StandardResult>
    {
        public StandardResult Handle(IncrementTurnCommand cmd, ICommandScope scope)
        {
            scope.World.Match.Turn += 1;
            return StandardResult.Instance;
        }
    }

    public sealed class SwapActivePlayerHandler : ICommandHandler<SwapActivePlayerCommand, StandardResult>
    {
        public StandardResult Handle(SwapActivePlayerCommand cmd, ICommandScope scope)
        {
            scope.World.Match.SwapActivePlayer();
            return StandardResult.Instance;
        }
    }

    public sealed class UntapAllHandler : ICommandHandler<UntapAllCommand, StandardResult>
    {
        public StandardResult Handle(UntapAllCommand cmd, ICommandScope scope) => StandardResult.Instance;
    }

    public sealed class DrawCardHandler : ICommandHandler<DrawCardCommand, StandardResult>
    {
        public StandardResult Handle(DrawCardCommand cmd, ICommandScope scope) => StandardResult.Instance;
    }
}
