namespace Framework.Pipeline.Tests
{
    public sealed class DeniedValidator : Validator<Message, Denied, TestView>
    {
        // InternalsVisibleTo makes this assembly a friend; an author assembly writes "protected override".
        protected internal override bool Validate(Denied message, TestView view, out string reason)
        {
            reason = view.Mana > 0 ? null : "no mana";
            return reason == null;
        }
    }
}
