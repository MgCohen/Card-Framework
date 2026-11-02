using System;

namespace Framework.Pipeline.Tests
{
    public sealed class CrashValidator : Validator<Message, Crash, TestView>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Crash message, TestView view, out string reason)
            => throw new InvalidOperationException("crash");
    }
}
