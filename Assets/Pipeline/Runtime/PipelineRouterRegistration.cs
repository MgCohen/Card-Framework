using System;
using Framework.Routing;
using VContainer;

namespace Framework.Pipeline
{
    // Registers one TypeRouter<T> singleton and keeps its scan option, so a second
    // AddPipeline call for the same router can check that it asks for the same scan.
    sealed class PipelineRouterRegistration<T> : RegistrationBuilder where T : class, IRouted
    {
        readonly bool _includeTests;

        public PipelineRouterRegistration(bool includeTests) : base(typeof(TypeRouter<T>), Lifetime.Singleton)
        {
            _includeTests = includeTests;
            WithParameter(RouteMode.Single);
        }

        public override Registration Build()
        {
            var inner = base.Build();
            return new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this, inner.Provider), Key);
        }

        public void Match(bool includeTests)
        {
            if (includeTests != _includeTests)
                throw new InvalidOperationException(
                    $"The router {typeof(T).Name} has IncludeTests = {_includeTests}; a second AddPipeline call asks for {includeTests}.");
        }

        public static PipelineRouterRegistration<T> InParents(IObjectResolver scope)
        {
            for (var parent = (scope as IScopedObjectResolver)?.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.TryGetRegistration(typeof(TypeRouter<T>), out var registration))
                    return (registration.Provider as Provider)?.Router;
            }
            return null;
        }

        sealed class Provider : IInstanceProvider
        {
            readonly IInstanceProvider _inner;

            public Provider(PipelineRouterRegistration<T> router, IInstanceProvider inner)
            {
                Router = router;
                _inner = inner;
            }

            public PipelineRouterRegistration<T> Router { get; }

            public object SpawnInstance(IObjectResolver resolver) => _inner.SpawnInstance(resolver);
        }
    }
}
