using System;
using System.Collections.Generic;
using System.Linq;

namespace Tools.Rulebook
{
    public static class CheckRegistry
    {
        static readonly IRuleCheck[] All =
        {
            new AsmdefNoCyclesCheck(),
            new AsmdefAllowedReferencesCheck(),
            new AsmdefForbiddenReferencesCheck(),
            new IvtAllowlistCheck(),
            new IlCallsCheck(IlCallsCheck.CallersOnly),
            new IlCallsCheck(IlCallsCheck.ForbiddenCalls),
            new PropertyTypesCheck(),
            new NoSubtypesCheck(),
            new FeatureHasTestsCheck(),
            new RequireAttributeCheck(),
            new AttachedCheck(),
        };

        public static IReadOnlyCollection<string> Kinds { get; } = All.Select(c => c.Kind).ToArray();

        public static IRuleCheck For(string kind)
            => All.FirstOrDefault(c => c.Kind == kind)
               ?? throw new ArgumentException($"Unknown check kind '{kind}'.");
    }
}
