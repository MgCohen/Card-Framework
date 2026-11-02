using VContainer;

namespace Framework.Kinds
{
    // Registers one KindsOptions instance, so a second AddKinds call changes the same options.
    sealed class KindsOptionsRegistration : RegistrationBuilder
    {
        public KindsOptionsRegistration() : base(typeof(KindsOptions), Lifetime.Singleton) { }

        public KindsOptions Options { get; } = new();

        public override Registration Build() =>
            new Registration(ImplementationType, Lifetime, InterfaceTypes, new Provider(Options), Key);

        sealed class Provider : IInstanceProvider
        {
            readonly KindsOptions _options;

            public Provider(KindsOptions options) => _options = options;

            public object SpawnInstance(IObjectResolver resolver) => _options;
        }
    }
}
