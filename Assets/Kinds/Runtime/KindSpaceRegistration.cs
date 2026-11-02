using System;
using VContainer;

namespace Framework.Kinds
{
    // Registers one TRegistry singleton and keeps the data actions, so a second
    // AddKindSpace call for the same registry adds its data to the first call.
    sealed class KindSpaceRegistration<TKey, TRegistry> : RegistrationBuilder, IKindSpaceRegistration
        where TKey : TypeKey where TRegistry : TypeKey.Registry<TKey>, new()
    {
        Action<TRegistry> _data;

        public KindSpaceRegistration() : base(typeof(TRegistry), Lifetime.Singleton) { }

        public Type KeyType => typeof(TKey);

        public Type RegistryType => typeof(TRegistry);

        public void AddData(Action<TRegistry> data) => _data += data;

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(this), Key);

        TRegistry Create(KindsOptions options)
        {
            var registry = new TRegistry();
            registry.RegisterCodeMembers(options.IncludeTests);
            _data?.Invoke(registry);
            registry.Freeze();
            if (options.Validate)
            {
                var issues = KindValidator.Validate(options.IncludeTests, typeof(TKey));
                if (issues.Count > 0)
                    throw new InvalidOperationException($"{typeof(TRegistry).Name}: {string.Join(" ", issues)}");
            }
            return registry;
        }

        sealed class Provider : IInstanceProvider
        {
            readonly KindSpaceRegistration<TKey, TRegistry> _space;

            public Provider(KindSpaceRegistration<TKey, TRegistry> space) => _space = space;

            public object SpawnInstance(IObjectResolver resolver) =>
                _space.Create(resolver.ResolveOrDefault<KindsOptions>() ?? new KindsOptions());
        }
    }

    // Lets AddKindSpace find a space for the same key with a different registry type.
    interface IKindSpaceRegistration
    {
        Type KeyType { get; }
        Type RegistryType { get; }
    }
}
