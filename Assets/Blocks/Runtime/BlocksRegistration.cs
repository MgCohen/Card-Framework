using System;
using Framework.RecordIds;
using Framework.Wire;
using VContainer;

namespace Framework.Blocks
{
    // Registers the BlockPositions singleton and keeps the Role, so a second
    // AddBlocks call can check that it asks for the same role.
    sealed class BlocksRegistration : RegistrationBuilder
    {
        readonly Role _role;

        public BlocksRegistration(Role role) : base(typeof(BlockPositions), Lifetime.Singleton) => _role = role;

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this), Key);

        public void Match(Role role)
        {
            if (role != _role)
                throw new InvalidOperationException(
                    $"Blocks is installed as {_role}; a second AddBlocks call asks for {role}.");
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

            public object SpawnInstance(IObjectResolver resolver) => new BlockPositions(resolver.Resolve<EventStream>());
        }
    }
}
