using System;
using VContainer;

namespace Framework.TypeKeys
{
    // Registers one TRegistry singleton and keeps the data actions, so a second
    // AddKeySpace call for the same registry adds its data to the first call.
    sealed class KeySpaceRegistration<TKey, TRegistry> : RegistrationBuilder, IKeySpaceRegistration
        where TKey : TypeKey where TRegistry : TypeKey.Registry<TKey>, new()
    {
        Action<TRegistry> _data;

        public KeySpaceRegistration() : base(typeof(TRegistry), Lifetime.Singleton) { }

        public Type KeyType => typeof(TKey);

        public Type RegistryType => typeof(TRegistry);

        public void AddData(Action<TRegistry> data) => _data += data;

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this), Key);

        TRegistry Create(TypeKeysOptions options)
        {
            var registry = new TRegistry();
            registry.RegisterCodeMembers(options.IncludeTests);
            _data?.Invoke(registry);
            registry.Freeze();
            if (options.Validate)
            {
                var issues = TypeKeyValidator.Validate(options.IncludeTests, typeof(TKey));
                if (issues.Count > 0)
                    throw new InvalidOperationException($"{typeof(TRegistry).Name}: {string.Join(" ", issues)}");
            }
            return registry;
        }

        sealed class Provider : IInstanceProvider
        {
            readonly KeySpaceRegistration<TKey, TRegistry> _space;

            public Provider(KeySpaceRegistration<TKey, TRegistry> space) => _space = space;

            public object SpawnInstance(IObjectResolver resolver) =>
                _space.Create(resolver.ResolveOrDefault<TypeKeysOptions>() ?? new TypeKeysOptions());
        }
    }

    // Lets AddKeySpace find a space for the same key with a different registry type.
    interface IKeySpaceRegistration
    {
        Type KeyType { get; }
        Type RegistryType { get; }
    }
}
