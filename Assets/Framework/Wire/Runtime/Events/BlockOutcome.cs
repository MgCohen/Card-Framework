namespace Framework.Wire
{
    public enum BlockOutcome : byte
    {
        None      = 0,
        Completed = 1,
        Errored   = 2,
        Cancelled = 3,
    }
}
