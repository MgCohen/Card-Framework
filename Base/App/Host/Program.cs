using VSA.Composition;
using VSA.Host;

await VsaBuilder.Create(args).UseWebHost().Build().RunAsync();
