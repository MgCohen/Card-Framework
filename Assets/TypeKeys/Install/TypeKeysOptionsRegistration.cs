using VContainer;

namespace Framework.TypeKeys
{
    // Registers one TypeKeysOptions instance, so a second AddTypeKeys call changes the same options.
    sealed class TypeKeysOptionsRegistration : RegistrationBuilder
    {
        public TypeKeysOptionsRegistration() : base(typeof(TypeKeysOptions), Lifetime.Singleton) { }

        public TypeKeysOptions Options { get; } = new();

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(Options), Key);

        sealed class Provider : IInstanceProvider
        {
            readonly TypeKeysOptions _options;

            public Provider(TypeKeysOptions options) => _options = options;

            public object SpawnInstance(IObjectResolver resolver) => _options;
        }
    }
}
