namespace Framework.Pipeline.Tests
{
    public sealed class TestView
    {
        public TestView(int mana = 0) => Mana = mana;

        public int Mana { get; }
    }
}
