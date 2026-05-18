#nullable enable
using System.Collections.Generic;

namespace Spike.Modifiers
{
    public interface IEntityDefinition
    {
        int        Id   { get; }
        EntityType Type { get; }
    }

    public enum CardKind { Minion, Spell }

    public sealed record CardDefinition(
        int      Id,
        string   Name,
        CardKind Kind,
        int      Cost,
        int      Attack,
        int      Health,
        int      Defense      = 0,
        bool     DivineShield = false
    ) : IEntityDefinition
    {
        public EntityType Type => EntityTypes.Card;
    }
}
