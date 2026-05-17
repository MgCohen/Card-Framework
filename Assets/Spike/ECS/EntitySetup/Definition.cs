#nullable enable
using System.Collections.Generic;

namespace Spike.EntitySetup
{
    /// <summary>
    /// The base contract every definition implements. Minimum surface
    /// today: <see cref="Id"/> + <see cref="Type"/>. Base parameters that
    /// every def shares (a name? a base-tag enumeration?) earn their
    /// place here when the pressure justifies it — parked for now.
    /// </summary>
    public interface IEntityDefinition
    {
        int        Id   { get; }
        EntityType Type { get; }
    }

    /// <summary>
    /// Card sub-kind. Closed framework-internal set — enum is fine here
    /// (not extensible by games per the open-vs-closed type rule). When
    /// a game needs a kind the framework doesn't ship, the right move is
    /// a different <see cref="EntityType"/> (e.g. Token, Hero), not a
    /// new CardKind value.
    /// </summary>
    public enum CardKind { Minion, Spell, Hero }

    /// <summary>
    /// Authored content for one card. Pure C# record — the runtime
    /// currency that the catalog stores and that <c>SpawnCard</c>
    /// consumes. A ScriptableObject wrapper would sit on top of this for
    /// editor authoring (out of scope for the spike — see the spike doc).
    /// </summary>
    public sealed record CardDefinition(
        int      Id,
        string   Name,
        CardKind Kind,
        int      Cost,
        int      Attack,
        int      Health,
        bool     DivineShield,
        IReadOnlyList<CardEffect> Effects
    ) : IEntityDefinition
    {
        public EntityType Type => EntityTypes.Card;
    }

    /// <summary>
    /// Placeholder for the data-asset shape effects will take. Real
    /// shape parked — see the modifier sketch and the spike doc for
    /// where this lands. The spike treats Effects as a flat list and
    /// doesn't register them anywhere (no EffectStore).
    /// </summary>
    public sealed record CardEffect(string Description);
}
