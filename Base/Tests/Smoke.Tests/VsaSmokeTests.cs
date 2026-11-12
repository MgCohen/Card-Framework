using VSA.Testing.Unit;

namespace Smoke.Tests;

public sealed class VsaSmokeTests : VsaUnitTest
{
    [Test]
    public void VsaTestingUnit_Loads()
    {
        Assert.That(Events, Is.Not.Null);
    }
}
