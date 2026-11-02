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
                if (builder[i] is KindSpaceRegistration<TKey, TRegistry> known)
                {
                    known.AddData(data);
                    return builder;
                }
            }

            if (builder.Exists(typeof(TRegistry), findParentScopes: true))
            {
                // A parent scope has the space, and it is frozen: a child scope cannot add data to it.
                if (data != null)
                    throw new InvalidOperationException(
                        $"{typeof(TRegistry).Name} is registered in a parent scope. Give the data to the parent AddKindSpace call.");
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
    }
}
