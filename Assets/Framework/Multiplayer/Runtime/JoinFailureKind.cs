namespace Framework.Multiplayer
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
