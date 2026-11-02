using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Kinds.Tests
{
    public class TypeKeyRegistryTests
    {
        ColorRegistry _colors;

        [SetUp]
        public void SetUp()
        {
            _colors = new ColorRegistry();
            _colors.Register<Red>();
            _colors.Register<Green>();
        }

        [Test]
        public void Register_SlotClash_ThrowsAndNamesBothTypes()
        {
            var e = Assert.Throws<InvalidOperationException>(() => _colors.Define(1, s => new CustomColor(s)));

            Assert.That(e.Message, Does.Contain(nameof(Red)).And.Contain(nameof(CustomColor)).And.Contain("1"));
        }

        [Test]
        public void Register_EqualMember_DoesNothing()
        {
            _colors.Register(new Red());

            Assert.That(_colors.Members, Has.Count.EqualTo(2));
        }

        [Test]
        public void Register_AfterFreeze_Throws()
        {
            _colors.Freeze();

            Assert.Throws<InvalidOperationException>(() => _colors.Define(10, s => new CustomColor(s)));
            Assert.Throws<InvalidOperationException>(() => _colors.Register<Red>());
        }

        [Test]
        public void Define_ReturnsARegisteredMember()
        {
            var custom = _colors.Define(10, s => new CustomColor(s));

            Assert.That(_colors.SlotOf(custom), Is.EqualTo(10));
            Assert.That(_colors.BySlot(10), Is.SameAs(custom));
            Assert.That(_colors.Members, Does.Contain(custom));
        }

        [Test]
        public void BySlot_OfSlotOf_ReturnsTheMember()
        {
            foreach (var member in _colors.Members)
                Assert.That(_colors.BySlot(_colors.SlotOf(member)), Is.SameAs(member));
        }

        [Test]
        public void Of_ReturnsTheRegisteredMember()
        {
            Assert.That(_colors.Of<Red>(), Is.EqualTo(new Red()));
            Assert.That(_colors.SlotOf(_colors.Of<Green>()), Is.EqualTo(2));
        }

        [Test]
        public void Lookups_ForAMissingMember_FailClearly()
        {
            Assert.That(_colors.TryBySlot(99, out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() => _colors.BySlot(99));
            Assert.Throws<KeyNotFoundException>(() => _colors.Of<CustomColor>());
        }
    }
}
