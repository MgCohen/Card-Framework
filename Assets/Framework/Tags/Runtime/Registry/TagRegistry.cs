using Framework.Core;

namespace Framework.Tags
{
    // Registry of tag keys — a scoped instance (never a static global). A thin SlotRegistry<Tag>;
    // all behaviour is inherited (Register<T> / Register(member) / BySlot / Of<T>), including the
    // dup-slot guard. The framework ships no baseline tag keys — games register their own here.
    //
    // No runtime reader yet: while the framework is being built out this is the authoring and
    // slot-uniqueness surface. Value types T are recovered from the per-T wire channel, so the
    // store never needs the registry to rehydrate a key.
    public sealed class TagRegistry : SlotRegistry<Tag>
    {
    }
}
