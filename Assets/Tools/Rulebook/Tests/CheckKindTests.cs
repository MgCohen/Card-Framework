using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tools.Rulebook.Tests
{
    // Proves each check kind against Tools.Rulebook.Fixtures and Tools/Rulebook/fixtures: the
    // exact set of violations must come back, so a kind that stops detecting fails here.
    public class CheckKindTests
    {
        const string Fx  = "Tools.Rulebook.Fixtures";
        const string Dir = "Tools/Rulebook/fixtures";

        static string[] Run(string kind, string json)
        {
            using var context = new CheckContext();
            return CheckRegistry.For(kind).Run(JObject.Parse(json), context).Select(v => v.Target).OrderBy(t => t).ToArray();
        }

        static void AssertTargets(string[] actual, params string[] expected)
            => CollectionAssert.AreEquivalent(expected, actual, "violations: " + string.Join(" | ", actual));

        [Test]
        public void Asmdef_NoCycles_Finds_The_Cycle()
            => AssertTargets(Run("asmdef.noCycles", $"{{ 'roots': ['{Dir}/asmdefs'] }}"),
                             "Fx.A -> Fx.B -> Fx.C -> Fx.A");

        [Test]
        public void Asmdef_AllowedReferences_Finds_Unlisted_Reference_And_Unlisted_Assembly()
            => AssertTargets(Run("asmdef.allowedReferences", $@"{{
                    'roots': ['{Dir}/asmdefs'], 'scope': 'Fx.*',
                    'allowed': {{ 'Fx.A': ['Fx.B'], 'Fx.B': ['Fx.C'], 'Fx.C': [], 'Fx.D': ['Fx.A'] }} }}"),
                             "Fx.C -> Fx.A", "Fx.E");

        [Test]
        public void Asmdef_ForbiddenReferences_Finds_The_Spike_Reference()
            => AssertTargets(Run("asmdef.forbiddenReferences", $"{{ 'roots': ['{Dir}/asmdefs'], 'from': 'Fx.*', 'forbidden': ['Spike.*'] }}"),
                             "Fx.D -> Spike.Thing");

        [Test]
        public void Ivt_Allowlist_Finds_The_Extra_Grant()
            => AssertTargets(Run("ivt.allowlist", $"{{ 'assemblies': ['{Fx}'], 'allowed': {{ '{Fx}': ['Tools.Rulebook.Tests'] }} }}"),
                             $"{Fx} -> Not.Allowed");

        [Test]
        public void Il_CallersOnly_Finds_The_Service_Writing_A_Store()
            => AssertTargets(Run("il.callersOnly", $@"{{
                    'assemblies': ['{Fx}'],
                    'callees': {{ 'namespace': '{Fx}', 'type': '*Store', 'access': 'internal' }},
                    'allowedCallers': {{ 'subclassOf': '{Fx}.FakeHandler`1' }} }}"),
                             $"{Fx}.BadService::Cheat -> {Fx}.FakeStore::Write");

        [Test]
        public void Il_ForbiddenCalls_Finds_The_Handler_Calling_A_Service()
            => AssertTargets(Run("il.forbiddenCalls", $@"{{
                    'assemblies': ['{Fx}'],
                    'callers': {{ 'subclassOf': '{Fx}.FakeHandler`1' }},
                    'callees': {{ 'namespace': '{Fx}', 'type': '*Service' }} }}"),
                             $"{Fx}.ChattyHandler::Apply -> {Fx}.FakeService::Do");

        [Test]
        public void Type_PropertyTypes_Finds_The_Writable_Store()
            => AssertTargets(Run("type.propertyTypes", $"{{ 'assembly': '{Fx}', 'type': '{Fx}.FakeEngine', 'allowed': ['IReadOnly*', '*Service'] }}"),
                             "FakeEngine.Store");

        [Test]
        public void Type_NoSubtypesIn_Finds_The_Domain_Event()
            => AssertTargets(Run("type.noSubtypesIn", $"{{ 'assembly': '{Fx}', 'baseType': '{Fx}.FakeEvent', 'allowed': ['{Fx}.Block*'] }}"),
                             $"{Fx}.DomainFakeEvent");

        [Test]
        public void Feature_HasTests_Finds_Empty_And_Missing_Test_Assemblies()
            => AssertTargets(Run("feature.hasTests", $"{{ 'root': '{Dir}/features', 'runtimeFolder': 'Runtime', 'testFolders': ['Tests'] }}"),
                             "Beta", "Gamma");

        [Test]
        public void Tests_RequireAttribute_Finds_Unbound_Broken_And_Orphan()
            => AssertTargets(Run("tests.requireAttribute", $@"{{
                    'assemblies': ['{Fx}'], 'attribute': '{Fx}.FakePictureAttribute',
                    'testAttributes': ['{Fx}.FakeTestAttribute'],
                    'fileFolder': '{Dir}/scenarios', 'filePattern': '*.md', 'ignoreFilePrefix': '_',
                    'requireAllFilesReferenced': true }}"),
                             $"{Fx}.FakeBindings.Unbound", $"{Fx}.FakeBindings.Broken", "orphan.md");

        [Test]
        public void Attached_Finds_A_Rule_With_No_Linked_Test()
        {
            AssertTargets(Run("attached", "{ '$ruleId': 'NOPE-99' }"), "NOPE-99");
            AssertTargets(Run("attached", "{ '$ruleId': 'TEST-02' }"));
        }

        [Test]
        public void Exception_Suppresses_A_Matching_Violation()
        {
            using var context = new CheckContext();
            var rule = ForbiddenRule(new Exemption { Target = "Fx.D -> *", Reason = "fixture", Until = "never" });

            var result = RuleRunner.Run(rule, context);

            Assert.IsTrue(result.Passed, result.Message());
            Assert.AreEqual(1, result.Exempted.Count);
        }

        [Test]
        public void Exception_That_Matches_Nothing_Fails_As_Stale()
        {
            using var context = new CheckContext();
            var rule = ForbiddenRule(new Exemption { Target = "Fx.Z -> *", Reason = "fixture", Until = "never" });

            var result = RuleRunner.Run(rule, context);

            Assert.IsFalse(result.Passed);
            Assert.AreEqual(1, result.Violations.Count);
            Assert.AreEqual(1, result.StaleExceptions.Count);
        }

        static Rule ForbiddenRule(Exemption exemption) => new()
        {
            Id = "FX-01", Title = "fixture", Fix = "fixture",
            Check = new Check
            {
                Kind = "asmdef.forbiddenReferences",
                Params = JObject.Parse($"{{ 'roots': ['{Dir}/asmdefs'], 'from': 'Fx.*', 'forbidden': ['Spike.*'] }}"),
            },
            Exceptions = { exemption },
        };
    }
}
