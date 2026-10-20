using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    // params: roots[]
    sealed class AsmdefNoCyclesCheck : IRuleCheck
    {
        public string Kind => "asmdef.noCycles";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var index = new AsmdefIndex(context, Params.Strings(p, "roots"));
            var graph = index.Entries.ToDictionary(e => e.Name, e => e.References);
            var state = new Dictionary<string, int>();   // 0 unseen, 1 on stack, 2 done
            var stack = new List<string>();
            var cycles = new List<Violation>();

            void Visit(string node)
            {
                state[node] = 1;
                stack.Add(node);
                foreach (var next in graph[node].Where(graph.ContainsKey))
                {
                    state.TryGetValue(next, out var s);
                    if (s == 1)
                    {
                        var loop = stack.Skip(stack.IndexOf(next)).Append(next).ToArray();
                        cycles.Add(new Violation(string.Join(" -> ", loop), "assembly references form a cycle",
                                                 index.Entries.First(e => e.Name == node).Path));
                    }
                    else if (s == 0) Visit(next);
                }
                stack.RemoveAt(stack.Count - 1);
                state[node] = 2;
            }

            foreach (var node in graph.Keys)
                if (!state.ContainsKey(node)) Visit(node);
            return cycles;
        }
    }

    // params: roots[], scope (glob of references that count), exclude[] (asmdefs to skip),
    //         allowed { assemblyName: [allowed references] }. An in-scope asmdef missing from
    //         the table is itself a violation, so a new assembly forces a table entry.
    sealed class AsmdefAllowedReferencesCheck : IRuleCheck
    {
        public string Kind => "asmdef.allowedReferences";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var scope   = Params.String(p, "scope");
            var exclude = Params.Strings(p, "exclude", required: false);
            var allowed = Params.Table(p, "allowed");

            foreach (var e in new AsmdefIndex(context, Params.Strings(p, "roots")).Entries)
            {
                if (!Glob.Match(scope, e.Name) || Glob.Any(exclude, e.Name)) continue;
                if (!allowed.TryGetValue(e.Name, out var ok))
                {
                    yield return new Violation(e.Name, "assembly is not listed in the allowed table", e.Path);
                    continue;
                }
                foreach (var r in e.References.Where(r => Glob.Match(scope, r) && !ok.Contains(r)))
                    yield return new Violation($"{e.Name} -> {r}", "reference is not allowed for this assembly", e.Path);
            }
        }
    }

    // params: roots[], from (glob of asmdefs checked), forbidden[] (globs of references)
    sealed class AsmdefForbiddenReferencesCheck : IRuleCheck
    {
        public string Kind => "asmdef.forbiddenReferences";

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var from      = Params.String(p, "from");
            var forbidden = Params.Strings(p, "forbidden");

            foreach (var e in new AsmdefIndex(context, Params.Strings(p, "roots")).Entries)
            {
                if (!Glob.Match(from, e.Name)) continue;
                foreach (var r in e.References.Where(r => Glob.Any(forbidden, r)))
                    yield return new Violation($"{e.Name} -> {r}", "reference is forbidden", e.Path);
            }
        }
    }
}
