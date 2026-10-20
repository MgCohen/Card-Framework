using System;
using NUnit.Framework;

namespace Tools.Rulebook
{
    // Links a hand-written test to a rule in Tools/Rulebook/rulebook.json. NUnit writes the
    // id into the results XML as the "Rule" property, which is how the gate maps results to rules.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RuleAttribute : PropertyAttribute
    {
        public const string PropertyName = "Rule";

        public string Id { get; }

        public RuleAttribute(string id) : base(PropertyName, id) => Id = id;
    }
}
