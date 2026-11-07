namespace TypeKeys.Domain;

public static class TypeKeyRegistries
{
    public static IReadOnlyList<TypeKeyRegistration> Build(bool includeTests, params Type[] roots)
    {
        if (roots.Length == 0)
        {
            roots = TypeKeyValidator.AllRoots(includeTests);
        }

        List<TypeKeyRegistration> registrations = new List<TypeKeyRegistration>();
        foreach (Type root in roots)
        {
            IReadOnlyList<string> issues = TypeKeyValidator.Validate(includeTests, root);
            if (issues.Count > 0)
            {
                throw new InvalidOperationException($"{root.Name}: {string.Join(" ", issues)}");
            }

            Type baseRegistry = typeof(TypeKeyRegistry<>).MakeGenericType(root);
            Type registryType = CustomRegistry(root, baseRegistry, includeTests) ?? baseRegistry;
            ICodeMemberScan registry = (ICodeMemberScan)Activator.CreateInstance(registryType)!;
            registry.RegisterCodeMembers(includeTests);
            registry.Freeze();

            registrations.Add(new TypeKeyRegistration(baseRegistry, registry));
            if (registryType != baseRegistry)
            {
                registrations.Add(new TypeKeyRegistration(registryType, registry));
            }
        }

        return registrations;
    }

    private static Type? CustomRegistry(Type root, Type baseRegistry, bool includeTests)
    {
        Type[] custom = TypeScan.ConcreteTypesOf(baseRegistry, includeTests).ToArray();
        if (custom.Length > 1)
        {
            throw new InvalidOperationException(
                $"{root.Name} has {custom.Length} registries: {string.Join(", ", custom.Select(t => t.Name))}. "
                + "A key root has one registry.");
        }

        return custom.Length == 1 ? custom[0] : null;
    }
}
