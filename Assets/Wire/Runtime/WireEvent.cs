using Framework.Stripping;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    // RequireDerived keeps each event type in a stripped player: WireRegistrar channel ids depend on the full set.
    [AutoPack]
    [RequireDerived]
    public abstract partial class WireEvent { }
}
