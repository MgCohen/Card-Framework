using Pipeline.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class ValidatorTests
{
    [Test]
    public void TwoValidators_ForOneCommand_Throw()
    {
        Log log = new Log();
        Validator[] validators = { new PingValidator(log), new PingValidator(log) };

        Assert.Throws<InvalidOperationException>(new Action(() => new PipelineValidators(validators)));
    }

    [Test]
    public void Validate_NoValidator_Accepts()
    {
        PipelineValidators validators = new PipelineValidators(Array.Empty<Validator>());

        Assert.That(validators.Validate(new Ping(), out string? reason), Is.True);
        Assert.That(reason, Is.Null);
    }

    [Test]
    public void Validate_Rejects_GivesTheReason()
    {
        PipelineValidators validators = new PipelineValidators(new Validator[] { new DeniedValidator(new Log(), new Mana()) });

        Assert.That(validators.Validate(new Denied(), out string? reason), Is.False);
        Assert.That(reason, Is.EqualTo("no mana"));
    }

    [Test]
    public void Find_SkipsTestAssemblies()
    {
        Assert.That(PipelineValidators.Find(includeTests: false), Has.No.Member(typeof(PingValidator)));
    }

    [Test]
    public void Find_IncludeTests_FindsTheFixtureValidators()
    {
        Assert.That(
            PipelineValidators.Find(includeTests: true),
            Is.SupersetOf(new[] { typeof(CrashValidator), typeof(DeniedValidator), typeof(PingValidator) }));
    }
}
