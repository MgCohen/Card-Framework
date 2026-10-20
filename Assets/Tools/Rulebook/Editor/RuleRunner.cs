using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    public sealed class RuleResult
    {
        [JsonProperty("rule")]            public string RuleId;
        [JsonProperty("title")]           public string Title;
        [JsonProperty("passed")]          public bool Passed;
        [JsonProperty("error")]           public string Error;
        [JsonProperty("violations")]      public List<Violation> Violations = new();
        [JsonProperty("exempted")]        public List<Violation> Exempted = new();
        [JsonProperty("staleExceptions")] public List<Exemption> StaleExceptions = new();
        [JsonProperty("fix")]             public string Fix;

        public string Message()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{RuleId} · {Title}");
            if (Error != null) sb.AppendLine($"Check error: {Error}");
            if (Violations.Count > 0)
            {
                sb.AppendLine($"{Violations.Count} violation(s):");
                foreach (var v in Violations) sb.AppendLine($"- {v}");
                sb.AppendLine($"Fix: {Fix}");
            }
            if (StaleExceptions.Count > 0)
            {
                sb.AppendLine($"{StaleExceptions.Count} exception(s) no longer match a violation. Remove them from the rulebook:");
                foreach (var e in StaleExceptions) sb.AppendLine($"- {e.Target} (until: {e.Until})");
            }
            return sb.ToString();
        }
    }

    public static class RuleRunner
    {
        // Injected into every check's params so a check can refer to its own rule.
        public const string RuleIdParam = "$ruleId";

        public static RuleResult Run(Rule rule, CheckContext context)
        {
            var result = new RuleResult { RuleId = rule.Id, Title = rule.Title, Fix = rule.Fix };
            List<Violation> found;
            try
            {
                var parameters = (JObject)(rule.Check.Params ?? new JObject()).DeepClone();
                parameters[RuleIdParam] = rule.Id;
                found = CheckRegistry.For(rule.Check.Kind).Run(parameters, context).ToList();
            }
            catch (Exception e)
            {
                result.Error = $"{e.GetType().Name}: {e.Message}";
                return result;
            }

            var used = new HashSet<Exemption>();
            foreach (var v in found)
            {
                var exemption = rule.Exceptions.FirstOrDefault(e => Glob.Match(e.Target, v.Target));
                if (exemption == null) { result.Violations.Add(v); continue; }
                used.Add(exemption);
                result.Exempted.Add(v);
            }
            result.StaleExceptions = rule.Exceptions.Where(e => !used.Contains(e)).ToList();
            result.Passed = result.Violations.Count == 0 && result.StaleExceptions.Count == 0;
            return result;
        }

        // One file per rule; the gate merges them into report.json.
        public static void Write(RuleResult result)
        {
            var dir = Path.Combine(ProjectPaths.GateOutput, "rules");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, result.RuleId + ".json"),
                              JsonConvert.SerializeObject(result, Formatting.Indented));
        }
    }
}
