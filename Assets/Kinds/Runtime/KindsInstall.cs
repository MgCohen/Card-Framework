using System;
using VContainer;

namespace Framework.Kinds
{
    public static class KindsInstall
    {
        // Each system registers its own kind space, so two systems can register the same one.
        public static IContainerBuilder AddKindSpace<TKey, TRegistry>(this IContainerBuilder builder, Action<TRegistry> data = null)
            where TKey : TypeKey where TRegistry : TypeKey.Registry<TKey>, new()
        {
            for (var i = 0; i < builder.Count; i++)
            {
                if (builder[i] is IKindSpaceRegistration known && known.KeyType == typeof(TKey))
                {
                    if (known.RegistryType != typeof(TRegistry))
                        throw Mismatch<TKey, TRegistry>(known.RegistryType);
                    ((KindSpaceRegistration<TKey, TRegistry>)known).AddData(data);
                    return builder;
                }
            }

            // Every space also registers as TypeKey.Registry<TKey>, so this finds a parent space of any registry type.
            if (builder.Exists(typeof(TypeKey.Registry<TKey>), includeInterfaceTypes: true, findParentScopes: true))
            {
                // A parent scope has the space, and it is frozen: a child scope cannot add data to it.
                if (data != null)
                    throw new InvalidOperationException(
                        $"{typeof(TRegistry).Name} is registered in a parent scope. Give the data to the parent AddKindSpace call.");
                // The parent registry type is only readable from the built scope.
                builder.RegisterBuildCallback(scope =>
                {
                    var parentType = ParentRegistryType<TKey>(scope);
                    if (parentType != null && parentType != typeof(TRegistry))
                        throw Mismatch<TKey, TRegistry>(parentType);
                });
                return builder;
            }

            var space = builder.Register(new KindSpaceRegistration<TKey, TRegistry>());
            space.AddData(data);
            space.AsSelf();
            if (typeof(TRegistry) != typeof(TypeKey.Registry<TKey>))
                space.As<TypeKey.Registry<TKey>>();
            builder.RegisterBuildCallback(r => r.Resolve<TRegistry>());
            return builder;
        }

        public static IContainerBuilder AddKinds(this IContainerBuilder builder, Action<KindsOptions> configure)
        {
            KindsOptionsRegistration options = null;
            for (var i = 0; i < builder.Count && options == null; i++)
                options = builder[i] as KindsOptionsRegistration;
            options ??= builder.Register(new KindsOptionsRegistration());

            configure(options.Options);
            return builder;
        }

        static Type ParentRegistryType<TKey>(IObjectResolver scope) where TKey : TypeKey
        {
            for (var parent = (scope as IScopedObjectResolver)?.Parent; parent != null; parent = parent.Parent)
                if (parent.TryGetRegistration(typeof(TypeKey.Registry<TKey>), out var registration))
                    return registration.ImplementationType;
            return null;
        }

        static InvalidOperationException Mismatch<TKey, TRegistry>(Type other) =>
            new($"The kind space {typeof(TKey).Name} already has the registry {other.Name}; AddKindSpace asks for {typeof(TRegistry).Name}.");
    }
}
