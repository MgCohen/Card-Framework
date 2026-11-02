using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VContainer;

namespace Framework.Kinds
{
    public static class KindsInstall
    {
        // Per-builder state, so a repeated call for one space merges into the first call.
        static readonly ConditionalWeakTable<IContainerBuilder, BuilderState> States = new();

        public static IContainerBuilder AddKindSpace<TKey, TRegistry>(this IContainerBuilder builder, Action<TRegistry> data = null)
            where TKey : TypeKey where TRegistry : TypeKey.Registry<TKey>, new()
        {
            var state = States.GetOrCreateValue(builder);
            if (state.Spaces.TryGetValue(typeof(TRegistry), out var known))
            {
                ((Space<TKey, TRegistry>)known).Data += data;
                return builder;
            }

            var space = new Space<TKey, TRegistry> { Data = data };
            state.Spaces.Add(typeof(TRegistry), space);
            builder.Register(r => space.Build(r.ResolveOrDefault<KindsOptions>() ?? new KindsOptions()), Lifetime.Singleton);
            builder.RegisterBuildCallback(r => r.Resolve<TRegistry>());
            return builder;
        }

        public static IContainerBuilder AddKinds(this IContainerBuilder builder, Action<KindsOptions> configure)
        {
            var state = States.GetOrCreateValue(builder);
            if (state.Options == null)
            {
                state.Options = new KindsOptions();
                builder.RegisterInstance(state.Options);
            }
            configure(state.Options);
            return builder;
        }

        sealed class BuilderState
        {
            public readonly Dictionary<Type, object> Spaces = new();
            public KindsOptions Options;
        }

        sealed class Space<TKey, TRegistry> where TKey : TypeKey where TRegistry : TypeKey.Registry<TKey>, new()
        {
            public Action<TRegistry> Data;

            public TRegistry Build(KindsOptions options)
            {
                var registry = new TRegistry();
                registry.RegisterCodeMembers(options.IncludeTests);
                Data?.Invoke(registry);
                registry.Freeze();
                if (options.Validate)
                {
                    var issues = KindValidator.Validate(options.IncludeTests, typeof(TKey));
                    if (issues.Count > 0)
                        throw new InvalidOperationException($"{typeof(TRegistry).Name}: {string.Join(" ", issues)}");
                }
                return registry;
            }
        }
    }
}
