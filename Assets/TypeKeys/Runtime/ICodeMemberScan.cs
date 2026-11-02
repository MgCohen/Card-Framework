namespace Framework.TypeKeys
{
    // Lets TypeKeyValidator fill a registry when it knows the root only as a Type.
    internal interface ICodeMemberScan
    {
        void RegisterCodeMembers(bool includeTests);
    }
}
