namespace Framework.Commands
{
    // Thin, data-only outcome of Execute. Kept (over void) because Phase 1 adds rejection and
    // the eventual client-submission path needs accepted-vs-rejected-and-why; it is also the
    // seam the future evaluate-loop threads its outcome through. Factory methods (not an object
    // initializer) keep it a true readonly struct without an init-only dependency.
    public readonly struct CommandResult
    {
        public readonly bool   Accepted;
        public readonly string RejectReason;   // null when accepted

        CommandResult(bool accepted, string rejectReason)
        {
            Accepted     = accepted;
            RejectReason = rejectReason;
        }

        public static CommandResult Accept()              => new(true, null);
        public static CommandResult Reject(string reason) => new(false, reason);
    }
}
