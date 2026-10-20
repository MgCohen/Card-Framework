using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    // params: assemblies[], exclude[], allowed { assemblyName: [granted assembly names] }
    sealed class IvtAllowlistCheck : IRuleCheck
    {
        public string Kind => "ivt.allowlist";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var allowed = Params.Table(p, "allowed");
            foreach (var assembly in context.Select(Params.Strings(p, "assemblies"), Params.Strings(p, "exclude", required: false)))
            {
                var name = assembly.GetName().Name;
                allowed.TryGetValue(name, out var ok);
                foreach (var grant in assembly.GetCustomAttributes<InternalsVisibleToAttribute>())
                {
                    var to = grant.AssemblyName.Split(',')[0].Trim();
                    if (ok == null || !ok.Contains(to))
                        yield return new Violation($"{name} -> {to}", "InternalsVisibleTo grant is not in the allowlist");
                }
            }
        }
    }

    // params: assembly, type (full name), allowed[] (globs on property type names, generic arity removed)
    sealed class PropertyTypesCheck : IRuleCheck
    {
        public string Kind => "type.propertyTypes";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var type    = Reflect.Type(context, Params.String(p, "assembly"), Params.String(p, "type"));
            var allowed = Params.Strings(p, "allowed");
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var typeName = prop.PropertyType.Name.Split('`')[0];
                if (!Glob.Any(allowed, typeName))
                    yield return new Violation($"{type.Name}.{prop.Name}", $"public property type {typeName} is not allowed");
            }
        }
    }

    // params: assembly, baseType (full name), allowed[] (globs on full type names)
    sealed class NoSubtypesCheck : IRuleCheck
    {
        public string Kind => "type.noSubtypesIn";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var assembly = context.Loaded(Params.String(p, "assembly"))
                           ?? throw new ArgumentException($"assembly '{p["assembly"]}' is not loaded");
            var baseType = Reflect.FindType(context, Params.String(p, "baseType"));
            var allowed  = Params.Strings(p, "allowed", required: false);
            foreach (var t in CheckContext.TypesOf(assembly).Where(t => t != baseType && baseType.IsAssignableFrom(t)))
                if (!Glob.Any(allowed, t.FullName))
                    yield return new Violation(t.FullName, $"subtype of {baseType.Name} is not allowed in {assembly.GetName().Name}");
        }
    }

    // params: root, runtimeFolder, testFolders[], testAttributes[] (optional)
    // Violation target: the feature folder name.
    sealed class FeatureHasTestsCheck : IRuleCheck
    {
        public string Kind => "feature.hasTests";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var root        = context.Full(Params.String(p, "root"));
            var runtime     = Params.String(p, "runtimeFolder");
            var testFolders = Params.Strings(p, "testFolders");
            var attributes  = Reflect.TestAttributes(p);

            foreach (var feature in Directory.GetDirectories(root).OrderBy(d => d))
            {
                if (!Directory.Exists(Path.Combine(feature, runtime))) continue;
                var name = Path.GetFileName(feature);

                var asmdefs = testFolders.Select(f => Path.Combine(feature, f))
                                         .Where(Directory.Exists)
                                         .SelectMany(d => Directory.GetFiles(d, "*.asmdef", SearchOption.AllDirectories))
                                         .ToList();
                if (asmdefs.Count == 0)
                {
                    yield return new Violation(name, "feature has no test assembly", ProjectPaths.Relative(feature));
                    continue;
                }

                var count = asmdefs.Select(f => JObject.Parse(File.ReadAllText(f)).Value<string>("name"))
                                   .Select(context.Loaded)
                                   .Where(a => a != null)
                                   .Sum(a => Reflect.TestMethods(a, attributes).Count());
                if (count == 0)
                    yield return new Violation(name, "feature test assemblies contain no tests", ProjectPaths.Relative(asmdefs[0]));
            }
        }
    }

    // params: assemblies[], attribute (full name), fileFolder, filePattern, ignoreFilePrefix,
    //         requireAllFilesReferenced, testAttributes[] (optional)
    // Each test method (or its class) must carry the attribute; its first string argument
    // names a file in fileFolder. Optionally every file must be named by some test.
    sealed class RequireAttributeCheck : IRuleCheck
    {
        public string Kind => "tests.requireAttribute";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var attribute  = Params.String(p, "attribute");
            var folder     = Params.String(p, "fileFolder");
            var pattern    = Params.String(p, "filePattern", "*");
            var ignore     = Params.String(p, "ignoreFilePrefix", "\0");
            var requireAll = Params.Bool(p, "requireAllFilesReferenced", false);
            var attributes = Reflect.TestAttributes(p);
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var assemblies = context.Select(Params.Strings(p, "assemblies"), null).ToList();
            if (assemblies.Count == 0) throw new ArgumentException("no loaded assembly matches 'assemblies'");

            foreach (var method in assemblies.SelectMany(a => Reflect.TestMethods(a, attributes)))
            {
                var target = $"{method.DeclaringType?.FullName}.{method.Name}";
                var arg = Reflect.FirstStringArg(method, attribute) ?? Reflect.FirstStringArg(method.DeclaringType, attribute);
                if (arg == null)
                {
                    yield return new Violation(target, $"test has no [{ShortName(attribute)}]");
                    continue;
                }
                referenced.Add(arg);
                if (!File.Exists(Path.Combine(context.Full(folder), arg)))
                    yield return new Violation(target, $"[{ShortName(attribute)}(\"{arg}\")] names a file that does not exist in {folder}");
            }

            if (!requireAll) yield break;
            foreach (var file in Directory.GetFiles(context.Full(folder), pattern).Select(Path.GetFileName).OrderBy(f => f))
                if (!file.StartsWith(ignore) && !referenced.Contains(file))
                    yield return new Violation(file, "file is not named by any test", $"{folder}/{file}");
        }

        static string ShortName(string fullName) => fullName.Split('.').Last().Replace("Attribute", "");
    }

    // A rule whose check is a hand-written test carrying [Rule("ID")]. This check only proves
    // the link exists; the linked tests run in their own gate stage.
    sealed class AttachedCheck : IRuleCheck
    {
        public string Kind => "attached";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var id = Params.String(p, RuleRunner.RuleIdParam);
            if (!TestsFor(id).Any())
                yield return new Violation(id, $"no test carries [Rule(\"{id}\")]");
        }

        public static IEnumerable<MethodInfo> TestsFor(string ruleId)
            => Reflect.AllRuleLinks().Where(l => l.ruleId == ruleId).Select(l => l.method);
    }

    static class Reflect
    {
        static readonly string[] DefaultTestAttributes =
        {
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseAttribute",
            "NUnit.Framework.TestCaseSourceAttribute",
            "UnityEngine.TestTools.UnityTestAttribute",
        };

        public static string[] TestAttributes(JObject p)
        {
            var custom = Params.Strings(p, "testAttributes", required: false);
            return custom.Length > 0 ? custom : DefaultTestAttributes;
        }

        public static Type FindType(CheckContext context, string fullName)
            => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null)
               ?? throw new ArgumentException($"type '{fullName}' is not loaded");

        public static Type Type(CheckContext context, string assembly, string fullName)
            => (context.Loaded(assembly) ?? throw new ArgumentException($"assembly '{assembly}' is not loaded"))
               .GetType(fullName, false) ?? throw new ArgumentException($"type '{fullName}' is not in {assembly}");

        public static IEnumerable<MethodInfo> TestMethods(Assembly assembly, string[] attributes)
            => CheckContext.TypesOf(assembly)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Where(m => m.CustomAttributes.Any(a => attributes.Contains(a.AttributeType.FullName)));

        public static string FirstStringArg(MemberInfo member, string attributeFullName)
        {
            if (member == null) return null;
            var data = member.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == attributeFullName);
            return data?.ConstructorArguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
        }

        // Every [Rule("ID")] on a method or class in any loaded assembly.
        public static IEnumerable<(string ruleId, MethodInfo method)> AllRuleLinks()
        {
            const string ruleAttribute = "Tools.Rulebook.RuleAttribute";
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic))
            {
                if (!assembly.GetReferencedAssemblies().Any(r => r.Name == "Tools.Rulebook") && assembly.GetName().Name != "Tools.Rulebook")
                    continue;
                foreach (var type in CheckContext.TypesOf(assembly))
                {
                    var classIds = type.CustomAttributes.Where(a => a.AttributeType.FullName == ruleAttribute)
                                       .Select(a => (string)a.ConstructorArguments[0].Value).ToArray();
                    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        var ids = m.CustomAttributes.Where(a => a.AttributeType.FullName == ruleAttribute)
                                   .Select(a => (string)a.ConstructorArguments[0].Value);
                        var isTest = m.CustomAttributes.Any(a => DefaultTestAttributes.Contains(a.AttributeType.FullName));
                        foreach (var id in ids.Concat(isTest ? classIds : Array.Empty<string>()).Distinct())
                            yield return (id, m);
                    }
                }
            }
        }
    }
}
