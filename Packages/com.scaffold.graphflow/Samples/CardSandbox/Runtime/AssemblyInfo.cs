#pragma warning disable SCA0009 // Assembly attributes use global namespace by design
using System.Runtime.CompilerServices;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.CardSandbox;

[assembly: InternalsVisibleTo("Scaffold.GraphFlow.CardSandbox.Tests")]

[assembly: GraphPackage(
    Runner = typeof(CardEffectRunner),
    Extension = "card",
    AssetMenu = "GraphFlow/Card",
    Convention = PortConvention.AllFieldsIn,
    RegistryNamespace = "Scaffold.GraphFlow.CardSandbox.Generated",
    DispatcherBase = typeof(CardCommandDispatcher<,>),
    CommandBase = typeof(Command<>))]
#pragma warning restore SCA0009
