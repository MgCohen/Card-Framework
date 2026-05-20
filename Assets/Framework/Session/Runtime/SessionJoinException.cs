using System;

namespace Framework.Session
{
    public sealed class SessionJoinException : Exception
    {
        public JoinFailureKind Reason { get; }

        public SessionJoinException(JoinFailureKind reason, string message) : base(message)
        {
            Reason = reason;
        }

        public SessionJoinException(JoinFailureKind reason, string message, Exception inner) : base(message, inner)
        {
            Reason = reason;
        }
    }
}
