using System.Reflection;
using TypeKeys.Domain.Shared;

namespace TypeKeys.Tests;

public sealed class TypeKeyTests
{
    private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

    [Test]
    public void PositionalMember_OverNonPositionalRoot_IsATypeKey()
    {
        TypeKey red = new Red();

        Assert.That(red, Is.InstanceOf<Color>().And.InstanceOf<ITypeKey>());
        Assert.That(typeof(Color).GetProperties(Public), Is.Empty);
    }

    [Test]
    public void Slot_IsAnInternalProperty_WithNoPublicReader()
    {
        PropertyInfo? slot = typeof(TypeKey).GetProperty("Slot", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(slot, Is.Not.Null);
        Assert.That(slot!.GetMethod!.IsAssembly, Is.True);
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
}
