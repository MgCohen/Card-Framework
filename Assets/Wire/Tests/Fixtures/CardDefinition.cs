namespace Framework.Wire.Tests
{
    public sealed class CardDefinition
    {
        public int Id { get; }
        public string Name { get; }

        public CardDefinition(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
