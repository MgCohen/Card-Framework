#nullable enable

namespace VerticalSlice.QueryBench.Model
{
    public enum ZoneId : byte { None = 0, Deck, Hand, Board, Graveyard }
    public enum PlayerId : byte { None = 0, P1, P2 }

    public static class BenchTags
    {
        public static readonly BenchTagKey<bool>     IsCard    = new("IsCard");
        public static readonly BenchTagKey<bool>     IsMinion  = new("IsMinion");
        public static readonly BenchTagKey<bool>     IsHero    = new("IsHero");
        public static readonly BenchTagKey<ZoneId>   Zone      = new("Zone");
        public static readonly BenchTagKey<PlayerId> Owner     = new("Owner");
        public static readonly BenchTagKey<int>      Attack    = new("Attack");
        public static readonly BenchTagKey<int>      Health    = new("Health");
    }
}
