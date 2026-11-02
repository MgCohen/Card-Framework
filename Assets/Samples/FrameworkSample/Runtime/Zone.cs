using Framework.TypeKeys;

namespace Framework.Samples.FrameworkSample
{
    public abstract record Zone : TypeKey
    {
        protected Zone(int slot) : base(slot) { }

        public sealed record Hand() : Zone(1);

        public sealed record Board() : Zone(2);
    }
}
