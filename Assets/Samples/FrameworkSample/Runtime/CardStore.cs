using Framework.RecordIds;

namespace Framework.Samples.FrameworkSample
{
    public sealed class CardStore : RecordStore<Card, CardId>
    {
        public void Put(Card card) => Write(card);
    }
}
