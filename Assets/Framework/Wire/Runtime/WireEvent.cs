using Scaffold.AutoPacker;

namespace Framework.Wire
{
    // Marker base for events that travel over the network.
    //
    // [AutoPack] on the abstract base makes the generator emit IPackable + IUnpackable
    // here (the polymorphic boxed surface) while skipping the typed IPackable<T> / IUnpackable<T>
    // (the base has no Packed struct). Leaf classes get those typed interfaces emitted on
    // themselves, closed over their own nested Packed type. NetworkChannel<T, TPacked>
    // calls the typed PackTyped/UnpackTyped directly via constraint, so the event itself
    // never implements WriteToNetwork/ReadFromNetwork.
    [AutoPack]
    public abstract partial class WireEvent { }
}
