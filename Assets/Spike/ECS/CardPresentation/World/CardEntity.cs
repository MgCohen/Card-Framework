#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Authoritative card record in the game world. Plain data — no
    /// behavior, no events. <see cref="World"/> owns the dictionary of
    /// these and fires <see cref="World.CardChanged"/> when one mutates;
    /// VMs read from here.
    ///
    /// <para>In a real engine wire-up this would be replaced by the
    /// engine's entity store. The fields here mirror what a card-shaped
    /// entity would expose: identity, current zone, display data, and
    /// intrinsic flags that override zone defaults.</para>
    /// </summary>
    public sealed class CardEntity
    {
        public int Id;
        public int ZoneId;

        public string Name = "";
        public CardKind Kind;
        public int Cost;
        public int? Attack;
        public int? Health;
        public string BodyText = "";

        // Per-card intrinsics — override zone defaults at configure time.
        public bool Revealed;
        public bool Frozen;
        public bool Pinned;
    }
}
