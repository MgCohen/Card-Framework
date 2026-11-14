namespace TypeKeys.Tests;

public sealed class RegisterCodeMembersTests
{
    [Test]
    public void RegisterCodeMembers_FindsTheSealedParameterlessMembers()
    {
        ColorRegistry colors = new ColorRegistry();
        colors.RegisterCodeMembers(includeTests: true);

        Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green() }));
        Assert.That(colors.Of<Red>(), Is.EqualTo(new Red()));
    }

    [Test]
    public void RegisterCodeMembers_SkipsADataMemberType()
    {
        ColorRegistry colors = new ColorRegistry();
        colors.RegisterCodeMembers(includeTests: true);

        Assert.That(colors.Members, Has.None.InstanceOf<CustomColor>());
    }

    [Test]
    public void RegisterCodeMembers_WithoutTests_FindsNothingInATestAssembly()
    {
        ColorRegistry colors = new ColorRegistry();
        colors.RegisterCodeMembers(includeTests: false);

        Assert.That(colors.Members, Is.Empty);
    }

    [Test]
    public void RegisterCodeMembers_Twice_KeepsTheSameMembers()
    {
        ColorRegistry colors = new ColorRegistry();
        colors.RegisterCodeMembers(includeTests: true);
        colors.RegisterCodeMembers(includeTests: true);

        Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green() }));
    }

    [Test]
    public void RegisterCodeMembers_AfterFreeze_Throws()
    {
        ColorRegistry colors = new ColorRegistry();
        colors.Freeze();

        Assert.Throws<InvalidOperationException>(new Action(() => colors.RegisterCodeMembers(includeTests: true)));
    }
}
