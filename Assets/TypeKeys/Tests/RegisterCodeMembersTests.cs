using System;
using NUnit.Framework;

namespace Framework.TypeKeys.Tests
{
    public class RegisterCodeMembersTests
    {
        [Test]
        public void RegisterCodeMembers_FindsTheSealedParameterlessMembers()
        {
            var colors = new ColorRegistry();
            colors.RegisterCodeMembers(includeTests: true);

            Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green() }));
            Assert.That(colors.Of<Red>(), Is.EqualTo(new Red()));
        }

        [Test]
        public void RegisterCodeMembers_SkipsADataMemberType()
        {
            var colors = new ColorRegistry();
            colors.RegisterCodeMembers(includeTests: true);

            Assert.That(colors.Members, Has.None.InstanceOf<CustomColor>());
        }

        [Test]
        public void RegisterCodeMembers_WithoutTests_FindsNothingInATestAssembly()
        {
            var colors = new ColorRegistry();
            colors.RegisterCodeMembers(includeTests: false);

            Assert.That(colors.Members, Is.Empty);
        }

        [Test]
        public void RegisterCodeMembers_Twice_KeepsTheSameMembers()
        {
            var colors = new ColorRegistry();
            colors.RegisterCodeMembers(includeTests: true);
            colors.RegisterCodeMembers(includeTests: true);

            Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green() }));
        }

        [Test]
        public void RegisterCodeMembers_AfterFreeze_Throws()
        {
            var colors = new ColorRegistry();
            colors.Freeze();

            Assert.Throws<InvalidOperationException>(() => colors.RegisterCodeMembers(includeTests: true));
        }
    }
}
