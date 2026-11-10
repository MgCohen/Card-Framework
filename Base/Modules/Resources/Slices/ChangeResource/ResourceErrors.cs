using VSA.Contracts;

namespace Resources.Slices.ChangeResource;

public static class ResourceErrors
{
    public static readonly Error UnknownResource = new Error(
        "Resources.UnknownResource", "The Resource value is not Health or Mana.");
}
