using NUnit.Framework;

namespace Framework.Kinds.Tests
{
    public class KindValidatorTests
    {
        [Test]
        public void Validate_SlotClash_GivesKind01()
        {
            var issues = KindValidator.Validate(typeof(ClashKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-01").And.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void Validate_PublicSlotConstructor_GivesKind02()
        {
            var issues = KindValidator.Validate(typeof(OpenKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-02").And.Contain(nameof(OpenMember)));
        }

        [Test]
        public void Validate_PositionalRoot_GivesKind03()
        {
            var issues = KindValidator.Validate(typeof(PositionalKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-03").And.Contain(nameof(PositionalKind)));
        }

        [Test]
        public void Validate_GoodRoot_GivesNoIssue()
        {
            Assert.That(KindValidator.Validate(typeof(Color)), Is.Empty);
        }

        [Test]
        public void Validate_WithoutTests_DoesNotScanTestMembers()
        {
            Assert.That(KindValidator.Validate(false, typeof(ClashKind), typeof(OpenKind)), Is.Empty);
        }

        [Test]
        public void Validate_NotAKindRoot_GivesAnIssueAndDoesNotThrow()
        {
            Assert.That(KindValidator.Validate(typeof(string)), Has.Count.EqualTo(1));
        }
    }
}
