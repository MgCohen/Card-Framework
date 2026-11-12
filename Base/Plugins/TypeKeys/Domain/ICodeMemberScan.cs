namespace TypeKeys.Domain;

internal interface ICodeMemberScan
{
    void RegisterCodeMembers(bool includeTests);

    void Freeze();
}
