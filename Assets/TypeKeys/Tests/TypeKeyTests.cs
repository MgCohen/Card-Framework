using System.Linq;
using System.Reflection;
using Framework.TypeRouting;
using NUnit.Framework;

namespace Framework.TypeKeys.Tests
{
    public class TypeKeyTests
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        [Test]
        public void PositionalMember_OverNonPositionalRoot_IsATypeKey()
        {
            TypeKey red = new Red();

            Assert.That(red, Is.InstanceOf<Color>().And.InstanceOf<ITypeKey>());
            Assert.That(typeof(Color).GetProperties(Public), Is.Empty);
        }

        [Test]
        public void Slot_IsAPrivateField_WithNoPublicReader()
        {
            var slot = typeof(TypeKey).GetField("_slot", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.IsPrivate, Is.True);
            Assert.That(typeof(TypeKey).GetProperties(Public).Where(p => p.PropertyType == typeof(int)), Is.Empty);
        }

        [Test]
        public void Records_WithTheSameTypeAndSlot_AreEqual()
        {
            Assert.That(new Red(), Is.EqualTo(new Red()));
            Assert.That(new Red(), Is.Not.EqualTo(new Green()));
            Assert.That(new CustomColor(10), Is.EqualTo(new CustomColor(10)));
            Assert.That(new CustomColor(10), Is.Not.EqualTo(new CustomColor(11)));
        }

        [Test]
        public void ITypeKey_HasRequireImplementors()
        {
            Assert.That(typeof(ITypeKey).IsDefined(typeof(RequireImplementorsAttribute), false), Is.True);
        }
    }
}
