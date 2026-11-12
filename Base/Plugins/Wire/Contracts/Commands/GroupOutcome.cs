namespace Wire.Contracts.Commands;

public enum GroupOutcome : byte
{
    None = 0,
    Completed = 1,
    Errored = 2,
    Cancelled = 3,
    Waiting = 4,
}
