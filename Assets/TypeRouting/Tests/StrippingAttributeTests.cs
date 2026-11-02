using System;
using NUnit.Framework;

namespace Framework.TypeRouting.Tests
{
    public class StrippingAttributeTests
    {
        [Test]
        public void IRouted_HasRequireImplementors()
        {
            Assert.That(typeof(IRouted).IsDefined(typeof(RequireImplementorsAttribute), false), Is.True);
        }

        [TestCase(typeof(RequireImplementorsAttribute), "RequireImplementorsAttribute", AttributeTargets.Interface)]
        [TestCase(typeof(RequireDerivedAttribute), "RequireDerivedAttribute", AttributeTargets.Class)]
        public void Attribute_KeepsTheNameAndTargetTheLinkerReads(Type attribute, string name, AttributeTargets targets)
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(attribute, typeof(AttributeUsageAttribute));
            Assert.That(attribute.Name, Is.EqualTo(name));
            Assert.That(usage.ValidOn, Is.EqualTo(targets));
        }
    }
}
