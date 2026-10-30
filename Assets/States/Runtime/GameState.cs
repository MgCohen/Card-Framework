using Framework.Core;

namespace Framework.States
{
    public abstract record GameState(int Slot) : TypeKey(Slot);
}
