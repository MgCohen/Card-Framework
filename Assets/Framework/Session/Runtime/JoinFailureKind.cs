namespace Framework.Session
{
    public enum JoinFailureKind
    {
        NgoConnectFailed,
        Timeout,
        InvalidJoinCode,
        AllocationNotFound,
        RelayUnavailable,
    }
}
