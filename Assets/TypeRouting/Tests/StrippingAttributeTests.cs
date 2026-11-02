using Framework.Stripping;
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
    }
}
