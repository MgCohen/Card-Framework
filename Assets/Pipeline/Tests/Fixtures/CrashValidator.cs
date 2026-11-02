using System;

namespace Framework.Pipeline.Tests
{
    public sealed class CrashValidator : Validator<Message>
    {
        public override Type RouteType => typeof(Crash);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Message message, out string reason)
            => throw new InvalidOperationException("crash");
    }
}
