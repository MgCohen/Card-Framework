namespace Framework.Commands
{
    public readonly struct CommandResult
    {
        public readonly bool   Accepted;
        public readonly string RejectReason;

        CommandResult(bool accepted, string rejectReason)
        {
            Accepted     = accepted;
            RejectReason = rejectReason;
        }

        public static CommandResult Accept()              => new(true, null);
        public static CommandResult Reject(string reason) => new(false, reason);
    }
}
