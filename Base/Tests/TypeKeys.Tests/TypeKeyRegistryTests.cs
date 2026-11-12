namespace TypeKeys.Tests;

public sealed class TypeKeyRegistryTests
{
    private ColorRegistry colors = new ColorRegistry();

    [SetUp]
    public void SetUp()
    {
        colors = new ColorRegistry();
        colors.Register<Red>();
        colors.Register<Green>();
    }

    [Test]
    public void Define_SlotClash_ThrowsAndNamesBothTypes()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => colors.Define(1, s => new CustomColor(s))))!;

        Assert.That(e.Message, Does.Contain(nameof(Red)).And.Contain(nameof(CustomColor)).And.Contain("slot 1"));
    }

    [Test]
    public void Register_SlotClash_ThrowsAndNamesBothTypes()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => colors.Register(new CustomColor(2))))!;

        Assert.That(e.Message, Does.Contain(nameof(Green)).And.Contain(nameof(CustomColor)).And.Contain("slot 2"));
    }

    [Test]
    public void Register_DataMember_GetsASlotButNotOf()
    {
        CustomColor custom = new CustomColor(10);
        colors.Register(custom);

        Assert.That(colors.BySlot(10), Is.SameAs(custom));
        Assert.Throws<KeyNotFoundException>(new Action(() => colors.Of<CustomColor>()));
    }

    [Test]
    public void Members_AreSortedBySlot()
    {
        ColorRegistry sorted = new ColorRegistry();
        sorted.Define(10, s => new CustomColor(s));
        sorted.Register<Green>();
        sorted.Register<Red>();

        Assert.That(sorted.Members, Is.EqualTo(new Color[] { new Red(), new Green(), new CustomColor(10) }));
    }

    [Test]
    public void Register_EqualMember_DoesNothing()
    {
        colors.Register(new Red());

        Assert.That(colors.Members, Has.Count.EqualTo(2));
    }

    [Test]
    public void Register_AfterFreeze_Throws()
    {
        colors.Freeze();

        Assert.Throws<InvalidOperationException>(new Action(() => colors.Define(10, s => new CustomColor(s))));
        Assert.Throws<InvalidOperationException>(new Action(() => colors.Register<Red>()));
    }

    [Test]
    public void Register_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(new Action(() => colors.Register(null!)));
    }

    [Test]
    public void Define_ReturnsARegisteredMember()
    {
        CustomColor custom = colors.Define(10, s => new CustomColor(s));

        Assert.That(colors.SlotOf(custom), Is.EqualTo(10));
        Assert.That(colors.BySlot(10), Is.SameAs(custom));
        Assert.That(colors.Members, Does.Contain(custom));
    }

    [Test]
    public void BySlot_OfSlotOf_ReturnsTheMember()
    {
        foreach (Color member in colors.Members)
        {
            Assert.That(colors.BySlot(colors.SlotOf(member)), Is.SameAs(member));
        }
    }

    [Test]
    public void Of_ReturnsTheRegisteredMember()
    {
        Assert.That(colors.Of<Red>(), Is.EqualTo(new Red()));
        Assert.That(colors.SlotOf(colors.Of<Green>()), Is.EqualTo(2));
    }

    [Test]
    public void Lookups_ForAMissingMember_FailClearly()
    {
        Assert.That(colors.TryBySlot(99, out _), Is.False);
        Assert.Throws<KeyNotFoundException>(new Action(() => colors.BySlot(99)));
        Assert.Throws<KeyNotFoundException>(new Action(() => colors.Of<CustomColor>()));
    }
}
