#nullable enable
using StateMachine.Engine;

namespace StateMachine.Sample
{
    /// <summary>
    /// The whole point of v6: data that "would have lived on a state record"
    /// lives here as entity tags instead. DeclareAttackersState doesn't
    /// carry an ImmutableList&lt;Ref&lt;Card&gt;&gt; Selected — instead each Creature
    /// gets the IsAttacking tag. The state predicate queries the world.
    /// </summary>
    public static class MtgTags
    {
        public static readonly TagKey<bool> IsAttacking     = TagRegistry.Allocate<bool>("IsAttacking");
        public static readonly TagKey<bool> DamageResolved  = TagRegistry.Allocate<bool>("DamageResolved");
        public static readonly TagKey<long> Blocking        = TagRegistry.Allocate<long>("Blocking");
        public static readonly TagKey<int>  Power           = TagRegistry.Allocate<int>("Power");
        public static readonly TagKey<int>  Toughness       = TagRegistry.Allocate<int>("Toughness");
        public static readonly TagKey<PlayerId> Controller  = TagRegistry.Allocate<PlayerId>("Controller");
    }
}
