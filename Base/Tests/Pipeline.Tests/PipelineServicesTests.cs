using Pipeline.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class PipelineServicesTests
{
    [Test]
    public void Resolve_CallsTheFunction()
    {
        Log log = new Log();
        PipelineServices services = new PipelineServices(type => type == typeof(Log) ? log : null);

        Assert.That(services.Resolve(typeof(Log)), Is.SameAs(log));
        Assert.That(services.Require<Log>(), Is.SameAs(log));
    }

    [Test]
    public void Require_Missing_ThrowsAndNamesTheType()
    {
        PipelineServices services = new PipelineServices(type => null);

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => services.Require<Log>()))!;

        Assert.That(e.Message, Does.Contain(nameof(Log)));
    }
}
