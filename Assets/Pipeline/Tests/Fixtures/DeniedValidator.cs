using System;

namespace Framework.Pipeline.Tests
{
    public sealed class DeniedValidator : Validator<Message>
    {
        public override Type RouteType => typeof(Denied);

        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Message message, out string reason)
        {
            reason = "no mana";
            return false;
        }
    }
}
