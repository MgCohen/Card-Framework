#nullable enable

namespace Spike.Modifiers
{
    public sealed record ModifierTooltip(
        string Label,
        string? Icon = null,
        string? Description = null);

    public sealed record ModifierDefinition(
        int              Id,
        string           Name,
        IModifierEffect? OnAttach = null,   // one-shot at spawn; transient writes
        IModifierEffect? Refresh  = null,   // every refresh; rebaseable writes
        int              Order    = 100,
        ModifierTooltip? Tooltip  = null
    ) : IEntityDefinition
    {
        public EntityType Type => EntityTypes.Modifier;
    }
}
