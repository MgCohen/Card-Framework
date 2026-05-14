#nullable enable
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Direct-trigger payload for the Deathrattle keyword. Carries the
    /// entity that died (the source of the deathrattle). Engine bridge:
    /// when MoveCard.Destination is a graveyard, the bridge calls
    /// <c>scope.FireDirect(card, new Deathrattle(card))</c>, which routes
    /// to whatever deathrattle effect that card has attached in the
    /// CardEffectStore (if any).
    ///
    /// "Deathrattle" is a direct trigger — only the dying card's own
    /// attached effect fires, not a broadcast. Compare with broadcast
    /// hooks (e.g., OnUnitDeath in the future) which would notify every
    /// subscribed effect across the world.
    ///
    /// Plain sealed class (not a C# record) because Unity's .NET surface
    /// doesn't ship <c>System.Runtime.CompilerServices.IsExternalInit</c>
    /// by default; records' init-setters require it.
    /// </summary>
    public sealed class Deathrattle
    {
        public IEntity Source { get; }
        public Deathrattle(IEntity source) { Source = source; }
    }
}
