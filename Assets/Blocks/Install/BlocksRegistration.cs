using System;
using Framework.RecordIds;
using Framework.Wire;
using VContainer;

namespace Framework.Blocks
{
    // Registers the BlockPositions singleton and keeps the options, so a second
    // AddBlocks call can check that it asks for the same options.
    sealed class BlocksRegistration : RegistrationBuilder
    {
        readonly Role _role;
        readonly bool _includeTests;

        public BlocksRegistration(BlocksOptions options) : base(typeof(BlockPositions), Lifetime.Singleton)
        {
            _role = options.Role;
            _includeTests = options.IncludeTests;
        }

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this), Key);

        public void Match(BlocksOptions options)
        {
            if (options.Role != _role)
                throw new InvalidOperationException(
                    $"Blocks is installed as {_role}; a second AddBlocks call asks for {options.Role}.");
            if (options.IncludeTests != _includeTests)
                throw new InvalidOperationException(
                    $"Blocks has IncludeTests = {_includeTests}; a second AddBlocks call asks for {options.IncludeTests}.");
        }

        public static BlocksRegistration InParents(IObjectResolver scope)
        {
            for (var parent = (scope as IScopedObjectResolver)?.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.TryGetRegistration(typeof(BlockPositions), out var registration))
                    return (registration.Provider as Provider)?.Blocks;
            }
            return null;
        }

        sealed class Provider : IInstanceProvider
        {
            public Provider(BlocksRegistration blocks) => Blocks = blocks;

            public BlocksRegistration Blocks { get; }

            // Warning: a child scope reuses the parent BlockPositions, which is bound to the parent
            // EventStream. An EventStream registered in the child scope gets no positions.
            public object SpawnInstance(IObjectResolver resolver) => new BlockPositions(resolver.Resolve<EventStream>());
        }
    }
}
