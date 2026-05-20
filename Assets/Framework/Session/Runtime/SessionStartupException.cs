using System;

namespace Framework.Session
{
    public sealed class SessionStartupException : Exception
    {
        public SessionStartupException(string message) : base(message) { }
        public SessionStartupException(string message, Exception inner) : base(message, inner) { }
    }
}
