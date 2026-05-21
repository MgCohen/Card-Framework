using System;

namespace Framework.Multiplayer
{
    public sealed class SessionStartupException : Exception
    {
        public SessionStartupException(string message) : base(message) { }
        public SessionStartupException(string message, Exception inner) : base(message, inner) { }
    }
}
