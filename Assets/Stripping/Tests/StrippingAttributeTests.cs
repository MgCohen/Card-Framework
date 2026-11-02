using System;
using NUnit.Framework;

namespace Framework.Stripping.Tests
{
    public class StrippingAttributeTests
    {
        [TestCase(typeof(RequireImplementorsAttribute), "RequireImplementorsAttribute", AttributeTargets.Interface)]
        [TestCase(typeof(RequireDerivedAttribute), "RequireDerivedAttribute", AttributeTargets.Class)]
        [TestCase(typeof(PreserveAttribute), "PreserveAttribute", AttributeTargets.All)]
        public void Attribute_KeepsTheNameAndTargetTheLinkerReads(Type attribute, string name, AttributeTargets targets)
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(attribute, typeof(AttributeUsageAttribute))
                        ?? new AttributeUsageAttribute(AttributeTargets.All);
            Assert.That(attribute.Name, Is.EqualTo(name));
            Assert.That(usage.ValidOn, Is.EqualTo(targets));
        }
    }
}
