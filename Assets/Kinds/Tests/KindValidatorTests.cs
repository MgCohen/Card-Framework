using NUnit.Framework;

namespace Framework.Kinds.Tests
{
    public class KindValidatorTests
    {
        [Test]
        public void Validate_SlotClash_GivesKind01()
        {
            var issues = KindValidator.Validate(true, typeof(ClashKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-01").And.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void Validate_PublicSlotConstructor_GivesKind02()
        {
            var issues = KindValidator.Validate(true, typeof(OpenKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-02").And.Contain(nameof(OpenMember)));
        }

        [Test]
        public void Validate_PositionalRoot_GivesKind03()
        {
            var issues = KindValidator.Validate(true, typeof(PositionalKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-03").And.Contain(nameof(PositionalKind)));
        }

        [Test]
        public void Validate_MemberConstructorThrows_GivesKind01AndDoesNotThrow()
        {
            var issues = KindValidator.Validate(true, typeof(ThrowKind));

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0], Does.StartWith("KIND-01").And.Contain("ThrowMember constructor failed."));
        }

        [Test]
        public void Validate_GoodRoot_GivesNoIssue()
        {
            Assert.That(KindValidator.Validate(true, typeof(Color)), Is.Empty);
        }

        [Test]
        public void Validate_Default_DoesNotScanTestMembers()
        {
            Assert.That(KindValidator.Validate(typeof(ClashKind), typeof(OpenKind)), Is.Empty);
        }

        [Test]
        public void Validate_NoRoots_ValidatesAllRoots()
        {
            var issues = KindValidator.Validate(true);

            Assert.That(issues, Has.Some.StartWith("KIND-01 ClashKind"));
            Assert.That(issues, Has.Some.StartWith("KIND-02 OpenKind"));
            Assert.That(issues, Has.Some.StartWith("KIND-01 ThrowKind"));
            Assert.That(issues, Has.None.Contain("Color"));
        }

        [Test]
        public void Validate_NotAKindRoot_GivesAnIssueAndDoesNotThrow()
        {
            var issues = KindValidator.Validate(typeof(string), null);

            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues[0], Does.Contain("not a kind root"));
            Assert.That(issues[1], Does.Contain("null"));
        }
    }
}
