namespace Framework.Wire
{
    internal interface IHandlerBucket
    {
        void DispatchUntyped(WireEvent ev);
    }
}
