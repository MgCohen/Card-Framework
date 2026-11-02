using System;
using VContainer;

namespace Framework.RecordIds
{
    // Registers one IdAllocator<TId> singleton and keeps its first value, so a second
    // AddIdSpace call for the same TId can check that it asks for the same space.
    sealed class IdSpaceRegistration<TId> : RegistrationBuilder where TId : Id, new()
    {
        readonly int _first;
        readonly IdAllocator<TId> _allocator;

        public IdSpaceRegistration(int first) : base(typeof(IdAllocator<TId>), Lifetime.Singleton)
        {
            _first = first;
            _allocator = new IdAllocator<TId>(first);
        }

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this), Key);

        public void Match(int first)
        {
            if (first != _first)
                throw new InvalidOperationException(
                    $"The id space {typeof(TId).Name} starts at {_first}; a second AddIdSpace call asks for {first}.");
        }

        public static IdSpaceRegistration<TId> InParents(IObjectResolver scope)
        {
            for (var parent = (scope as IScopedObjectResolver)?.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.TryGetRegistration(typeof(IdAllocator<TId>), out var registration))
                    return (registration.Provider as Provider)?.Space;
            }
            return null;
        }

        sealed class Provider : IInstanceProvider
        {
            public Provider(IdSpaceRegistration<TId> space) => Space = space;

            public IdSpaceRegistration<TId> Space { get; }

            public object SpawnInstance(IObjectResolver resolver) => Space._allocator;
        }
    }
}
