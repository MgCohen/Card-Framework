using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tools.Rulebook
{
    // '*' matches any run of characters, including dots. Matching is exact and case-sensitive.
    public static class Glob
    {
        static readonly Dictionary<string, Regex> Cache = new();

        public static bool Match(string pattern, string value)
        {
            if (value == null) return false;
            if (!Cache.TryGetValue(pattern, out var rx))
                Cache[pattern] = rx = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$");
            return rx.IsMatch(value);
        }

        public static bool Any(IEnumerable<string> patterns, string value)
            => patterns != null && patterns.Any(p => Match(p, value));
    }
}
