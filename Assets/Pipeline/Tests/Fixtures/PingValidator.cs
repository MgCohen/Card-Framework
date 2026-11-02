namespace Framework.Pipeline.Tests
{
    public sealed class PingValidator : Validator<Message, Ping, TestView>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Ping message, TestView view, out string reason)
        {
            reason = null;
            return true;
        }
    }
}
