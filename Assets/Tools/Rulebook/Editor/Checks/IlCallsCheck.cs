using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    // Scans IL with Mono.Cecil for calls from one set of types into another.
    //
    // il.callersOnly    params: assemblies[], exclude[], callees{namespace,type,access},
    //                           allowedCallers{subclassOf}, allowSelf (default true)
    //                   A call into a matching callee from any other type is a violation.
    // il.forbiddenCalls params: assemblies[], exclude[], callers{subclassOf}, callees{namespace,type,access}
    //                   A call from a matching caller into a matching callee is a violation.
    //
    // Violation target: "CallerType::Method -> CalleeType::Method".
    sealed class IlCallsCheck : IRuleCheck
    {
        public const string CallersOnly    = "il.callersOnly";
        public const string ForbiddenCalls = "il.forbiddenCalls";

        public string Kind { get; }

        public IlCallsCheck(string kind) => Kind = kind;

        public IEnumerable<Violation> Run(JObject p, CheckContext context)
        {
            var callees   = Params.Object(p, "callees");
            var calleeNs  = Params.String(callees, "namespace", "*");
            var calleeTy  = Params.String(callees, "type", "*");
            var access    = Params.String(callees, "access", "any");
            var callerSet = Params.Object(p, Kind == CallersOnly ? "allowedCallers" : "callers");
            var baseName  = Params.String(callerSet, "subclassOf");
            var allowSelf = Params.Bool(p, "allowSelf", true);

            var assemblies = context.Select(Params.Strings(p, "assemblies"), Params.Strings(p, "exclude", required: false)).ToList();
            if (assemblies.Count == 0) throw new ArgumentException("no loaded assembly matches 'assemblies'");

            foreach (var assembly in assemblies)
            {
                var module = context.Module(assembly.GetName().Name);
                if (module == null) throw new ArgumentException($"no IL found for {assembly.GetName().Name}");

                foreach (var type in AllTypes(module))
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    var owner = OwnerOf(type);
                    var callerMatches = IsSubclassOf(owner, baseName);
                    if (Kind == ForbiddenCalls && !callerMatches) continue;

                    foreach (var instr in method.Body.Instructions)
                    {
                        if (!(instr.Operand is MethodReference target)) continue;
                        MethodDefinition callee;
                        try { callee = target.Resolve(); }
                        catch { continue; }
                        if (callee == null) continue;

                        var calleeType = callee.DeclaringType;
                        if (!Glob.Match(calleeNs, calleeType.Namespace) || !Glob.Match(calleeTy, calleeType.Name)) continue;
                        if (access == "internal" && !(callee.IsAssembly || callee.IsFamilyAndAssembly)) continue;

                        if (Kind == CallersOnly)
                        {
                            if (callerMatches) continue;
                            if (allowSelf && owner.FullName == calleeType.FullName) continue;
                        }

                        yield return new Violation(
                            $"{owner.FullName}::{OwnerMethodName(type, method)} -> {calleeType.FullName}::{callee.Name}",
                            Kind == CallersOnly ? $"only a subclass of {baseName} may call this" : $"a subclass of {baseName} must not call this",
                            LocationOf(method, instr));
                    }
                }
            }
        }

        static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
        {
            var stack = new Stack<TypeDefinition>(module.Types);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                yield return t;
                foreach (var n in t.NestedTypes) stack.Push(n);
            }
        }

        // Lambdas and iterators compile into nested generated types; report the user-written type.
        static TypeDefinition OwnerOf(TypeDefinition type)
        {
            while (type.IsNested && IsGenerated(type)) type = type.DeclaringType;
            return type;
        }

        static string OwnerMethodName(TypeDefinition type, MethodDefinition method)
            => IsGenerated(type) || method.Name.StartsWith("<") ? method.Name.Split('>')[0].TrimStart('<') : method.Name;

        static bool IsGenerated(TypeDefinition type)
            => type.Name.StartsWith("<") || type.CustomAttributes.Any(a => a.AttributeType.Name == "CompilerGeneratedAttribute");

        static bool IsSubclassOf(TypeDefinition type, string baseFullName)
        {
            var current = type.BaseType;
            for (var depth = 0; current != null && depth < 32; depth++)
            {
                var name = current is GenericInstanceType g ? g.ElementType.FullName : current.FullName;
                if (name == baseFullName) return true;
                TypeDefinition resolved;
                try { resolved = current.Resolve(); }
                catch { return false; }
                current = resolved?.BaseType;
            }
            return false;
        }

        static string LocationOf(MethodDefinition method, Instruction instr)
        {
            if (!method.DebugInformation.HasSequencePoints) return null;
            SequencePoint best = null;
            foreach (var sp in method.DebugInformation.SequencePoints)
            {
                if (sp.Offset > instr.Offset) break;
                if (!sp.IsHidden) best = sp;
            }
            return best == null ? null : $"{ProjectPaths.Relative(best.Document.Url)}:{best.StartLine}";
        }
    }
}
