#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// "Move card X from zone A to zone B." A row in <see cref="GameSystem.ValidActions"/>.
    /// Player intents emitted from the view (drop gestures) have the same
    /// shape — drop = "I want this move"; system accepts iff a matching
    /// entry exists in the valid-actions list.
    ///
    /// <para>For the spike there's exactly one action type — "move card."
    /// A real game would have more (declare attack, target spell, …);
    /// they'd live as siblings under an Action base class or interface.</para>
    /// </summary>
    public readonly struct MoveAction
    {
        public readonly int CardId;
        public readonly int FromZoneId;
        public readonly int ToZoneId;

        public MoveAction(int cardId, int fromZoneId, int toZoneId)
        {
            CardId = cardId;
            FromZoneId = fromZoneId;
            ToZoneId = toZoneId;
        }
    }
}
