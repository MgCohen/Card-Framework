using System.Collections.Generic;
using NUnit.Framework;

namespace Tools.Rulebook.Tests
{
    // One test case per deterministic rule in Tools/Rulebook/rulebook.json. Adding a rule of an
    // existing check kind adds a test here with no code change. Agent rules run in the review stage.
    public class RulebookTests
    {
        static IEnumerable<TestCaseData> Rules()
        {
            foreach (var rule in RulebookLoader.Load().Rules)
            {
                if (CheckRegistry.IsExternal(rule.Check.Kind)) continue;   // judged by the gate's review stage
                var data = new TestCaseData(rule.Id)
                    .SetName($"{rule.Id} {rule.Title}")
                    .SetProperty(RuleAttribute.PropertyName, rule.Id);
                if (rule.IsPlanned) data.Ignore("planned: no check yet");
                yield return data;
            }
        }

        [TestCaseSource(nameof(Rules))]
        public void Rule(string id)
        {
            var rule = RulebookLoader.Load().Rules.Find(r => r.Id == id);
            using var context = new CheckContext();

            var result = RuleRunner.Run(rule, context);
            RuleRunner.Write(result);

            if (!result.Passed) Assert.Fail(result.Message());
        }
    }
}
