using NUnit.Framework;

namespace Framework.TypeKeys.Tests
{
    public class TypeKeyValidatorTests
    {
        [Test]
        public void Validate_SlotClash_GivesTypeKey01()
        {
            var issues = TypeKeyValidator.Validate(true, typeof(ClashKey));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("TYPEKEY-01").And.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void Validate_PublicSlotConstructor_GivesTypeKey02()
        {
            var issues = TypeKeyValidator.Validate(true, typeof(OpenKey));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("TYPEKEY-02").And.Contain(nameof(OpenMember)));
        }

        [Test]
        public void Validate_PositionalRoot_GivesTypeKey03()
        {
            var issues = TypeKeyValidator.Validate(true, typeof(PositionalKey));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("TYPEKEY-03").And.Contain(nameof(PositionalKey)));
        }

        [Test]
        public void Validate_MemberConstructorThrows_GivesTypeKey01AndDoesNotThrow()
        {
            var issues = TypeKeyValidator.Validate(true, typeof(ThrowKey));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("TYPEKEY-01").And.Contain("ThrowMember constructor failed."));
        }

        [Test]
        public void Validate_GoodRoot_GivesNoIssue()
        {
            Assert.That(TypeKeyValidator.Validate(true, typeof(Color)), Is.Empty);
        }

        [Test]
        public void Validate_Default_DoesNotScanTestMembers()
        {
            Assert.That(TypeKeyValidator.Validate(typeof(ClashKey), typeof(OpenKey)), Is.Empty);
        }

        [Test]
        public void Validate_NoRoots_ValidatesAllRoots()
        {
            var issues = TypeKeyValidator.Validate(true);

            Assert.That(issues, Has.Some.StartWith("TYPEKEY-01 ClashKey"));
            Assert.That(issues, Has.Some.StartWith("TYPEKEY-02 OpenKey"));
            Assert.That(issues, Has.Some.StartWith("TYPEKEY-01 ThrowKey"));
            Assert.That(issues, Has.None.Contain("Color"));
        }

        [Test]
        public void TypeKeyValidator_ProductionRoots_HaveNoIssues()
        {
            Assert.IsEmpty(TypeKeyValidator.Validate());
        }

        [Test]
        public void Validate_NotAKeyRoot_GivesAnIssueAndDoesNotThrow()
        {
            var issues = TypeKeyValidator.Validate(typeof(string), null);

            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues[0], Does.Contain("not a key root"));
            Assert.That(issues[1], Does.Contain("null"));
        }
    }
}
