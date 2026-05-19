namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Port discovery strategy for generated dispatcher/listener nodes (ExecPlan v2).
    /// </summary>
    public enum PortConvention
    {
        CommandResultPair,
        AttributedFields,
        MutableInReadOnlyOut,
        AllFieldsIn,
    }
}
