using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tools.Rulebook.Tests
{
    // Keeps the rulebook, its schema and the [Rule] links consistent with each other.
    public class MetaTests
    {
        [Test]
        public void Rulebook_Is_Valid()
        {
            var errors = RulebookLoader.Validate(RulebookLoader.Load());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Every_Rule_Link_Names_An_Existing_Rule()
        {
            var ids = RulebookLoader.Load().Rules.Select(r => r.Id).ToHashSet();
            var broken = Reflect.AllRuleLinks()
                .Where(l => !ids.Contains(l.ruleId))
                .Select(l => $"{l.method.DeclaringType?.FullName}.{l.method.Name} -> [Rule(\"{l.ruleId}\")]")
                .ToList();
            Assert.IsEmpty(broken, "These tests name rules that are not in rulebook.json:\n" + string.Join("\n", broken));
        }

        [Test]
        public void Schema_Lists_Exactly_The_Registered_Check_Kinds()
        {
            var schema = JObject.Parse(File.ReadAllText(Path.Combine(ProjectPaths.Root, "Tools/Rulebook/schema/rulebook.schema.json")));
            var kinds = schema.SelectToken("properties.rules.items.properties.check.properties.kind.enum").Values<string>();
            CollectionAssert.AreEquivalent(CheckRegistry.Kinds, kinds);
        }
    }
}
