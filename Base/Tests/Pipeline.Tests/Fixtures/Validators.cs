using Pipeline.Slices.Run;

namespace Pipeline.Tests;

public sealed class PingValidator : Validator<Ping>
{
    private readonly Log log;

    public PingValidator(Log log)
    {
        this.log = log;
    }

    protected internal override bool Validate(Ping command, out string? reason)
    {
        log.Steps.Add("validate");
        reason = null;
        return true;
    }
}

public sealed class DeniedValidator : Validator<Denied>
{
    private readonly Log log;
    private readonly Mana mana;

    public DeniedValidator(Log log, Mana mana)
    {
        this.log = log;
        this.mana = mana;
    }

    protected internal override bool Validate(Denied command, out string? reason)
    {
        log.Steps.Add("validate");
        reason = mana.Value > 0 ? null : "no mana";
        return reason == null;
    }
}

public sealed class CrashValidator : Validator<Crash>
{
    private readonly Log log;

    public CrashValidator(Log log)
    {
        this.log = log;
    }

    protected internal override bool Validate(Crash command, out string? reason)
    {
        log.Steps.Add("validate");
        throw new InvalidOperationException("crash");
    }
}
