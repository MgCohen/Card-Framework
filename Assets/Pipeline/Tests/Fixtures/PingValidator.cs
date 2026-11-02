using System;

namespace Framework.Pipeline.Tests
{
    public sealed class PingValidator : Validator<Message>
    {
        public override Type RouteType => typeof(Ping);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Message message, out string reason)
        {
            reason = null;
            return true;
        }
    }
}
